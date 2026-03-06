using Npgsql;
using SharedUtils;
using SharedUtils.DTOs;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Serilog;

namespace ServerApp;

class Program {
    static async Task Main(string[] args) {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/server-.txt", 
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try {
            int port = 8080;
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Log.Information("Server listening on port {Port}", port);
        
            while(true) {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Log.Information("Client connected from {RemoteEndPoint}", client.Client.RemoteEndPoint);

                _ = HandleClientAsync(client);
            }
        } finally {
            Log.CloseAndFlush();
        }
    }

    static async Task HandleClientAsync(TcpClient client) {
        string connectionString = "Host=db;Username=admin;Password=admin;Database=drus_aleksa";

        using(client)
        using(NetworkStream stream = client.GetStream())
        using(StreamReader reader = new StreamReader(stream))
        using(StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
        await using(var dbConnection = new NpgsqlConnection(connectionString)) { // this opens one connection for each client(even if the client doesnt use it all the time). Better solution would be ipmlementing connection poolling TODO
            try {
                await dbConnection.OpenAsync();
                Log.Debug("Database connection opened for client {RemoteEndPoint}", client.Client.RemoteEndPoint);

                byte[] serverSharedSecret;

                string? handshakeLine = await reader.ReadLineAsync();
                var request = JsonSerializer.Deserialize<HandshakeRequestDto>(handshakeLine!);

                if(request == null || request.PublicKey == null) {
                    Log.Error("Invalid handshake received from client");
                    return;
                }

                using(var serverDh = new DhKeyExchange()) {
                    serverSharedSecret = serverDh.DeriveSharedSecret(request.PublicKey);

                    var response = new HandshakeResponseDto {
                        IsAccepted = true,
                        PublicKey = serverDh.PublicKey
                    };
                    await writer.WriteLineAsync(JsonSerializer.Serialize(response));

                    string secretPreview = BitConverter.ToString(serverSharedSecret).Substring(0, 14);
                    Log.Information("Handshake successful with sensor {SensorId}, secret starts with: {SecretPreview}", request.SensorId, secretPreview);
                }

                string? encryptedLine;
                while((encryptedLine = await reader.ReadLineAsync()) != null) {
                    try {
                        string decryptedJson = AesEncryptionHelper.Decrypt(encryptedLine, serverSharedSecret);

                        var reading = JsonSerializer.Deserialize<SensorReadingDto>(decryptedJson);

                        if(reading != null) {
                            Log.Information("Received reading from {SensorId}: Temperature {Value}°C", reading.SensorId, reading.Value);
                            string sql = @"
                            INSERT INTO drus_log (var_id, var_value, log_timestamp)
                            SELECT id, @val, TO_TIMESTAMP(@ts)
                            FROM drus_variables
                            WHERE name = @sensorName;";

                            await using(var cmd = new NpgsqlCommand(sql, dbConnection)) {
                                cmd.Parameters.AddWithValue("val", reading.Value);
                                cmd.Parameters.AddWithValue("ts", reading.Timestamp);
                                cmd.Parameters.AddWithValue("sensorName", reading.SensorId);

                                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                                if(rowsAffected == 0) {
                                    Log.Warning("Could not save log for sensor {SensorId} - variable not found in database", reading.SensorId);
                                } else {
                                    Log.Debug("Log saved to database for sensor {SensorId}", reading.SensorId);
                                }
                            }
                            /*
                             Here I use sql query for alarms to prevent caching invalidation. Better solution would be making Rest API that would change max/min values in memory(that are saved in some dictionry from database(on startup of server)) AND in database at the same time, for sync purposes. TODO
                            Keeping this would make this server "stateless", which means there is nothing to care about when primary server crashes and 2ndary takes over since everything is taken care of in database(lower performance the higher the scale)
                             */
                            string alarmSqll = @"
                                INSERT INTO drus_alarm(var_id, alarm_type, alarm_severity, triggered_value, message)
                                SELECT id,
                                CASE WHEN @val > max_safe_value THEN 'HIGH_LIMIT' ELSE 'LOW_LIMIT' END,
                                    'CRITICAL',
                                    @val,
                                    'Alarm! Sensor ' || @sensorName || ' has value: ' || @val || ' which is outside safe limits.'
                                FROM drus_variables
                                WHERE name = @sensorName AND (@val > max_safe_value OR @val < min_safe_value);";
                            await using(var alarmCmd = new NpgsqlCommand(alarmSqll, dbConnection)) {
                                alarmCmd.Parameters.AddWithValue("val", reading.Value);
                                alarmCmd.Parameters.AddWithValue("sensorName", reading.SensorId);

                                int alarmrsTriggered = await alarmCmd.ExecuteNonQueryAsync();

                                if(alarmrsTriggered > 0) {
                                    Log.Warning("ALARM TRIGGERED for sensor {SensorId} with value {Value}°C", reading.SensorId, reading.Value);
                                }
                            }
                        }
                    } catch(Exception ex) {
                        Log.Error(ex, "Error while reading/decrypting message from client");
                    }
                }
            } catch(Exception ex) {
                Log.Error(ex, "Connection lost with client {RemoteEndPoint}", client.Client.RemoteEndPoint);
            }
        }
        Log.Information("Sensor disconnected");
    }
}