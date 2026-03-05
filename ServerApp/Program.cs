using Npgsql;
using SharedUtils;
using SharedUtils.DTOs;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace ServerApp;

class Program {
    static async Task Main(string[] args) {
        int port = 8080;
        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Listening on port {port}...");
    
        while(true) {
            TcpClient client = await listener.AcceptTcpClientAsync();

            _ = HandleClientAsync(client);
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
                Console.WriteLine($"Connected to database for client {client.Client.RemoteEndPoint}");

                byte[] serverSharedSecret;

                string? handshakeLine = await reader.ReadLineAsync();
                var request = JsonSerializer.Deserialize<HandshakeRequestDto>(handshakeLine!);

                if(request == null || request.PublicKey == null) {
                    Console.WriteLine("Error: Invalid handshake...");
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
                    Console.WriteLine($"Handshake was  succsessfull! Sensor {request.SensorId} connected. Secret starts with: {secretPreview}...");
                }

                string? encryptedLine;
                while((encryptedLine = await reader.ReadLineAsync()) != null) {
                    try {
                        string decryptedJson = AesEncryptionHelper.Decrypt(encryptedLine, serverSharedSecret);

                        var reading = JsonSerializer.Deserialize<SensorReadingDto>(decryptedJson);

                        if(reading != null) {
                            Console.WriteLine($"Received reading: {reading.SensorId}, Temp: {reading.Value} C");
                            string sql = @"
                            INSERT INTO drus_log (var_id, val, created_at)
                            SELECT id, @val, TO_TIMESTAMP(@ts)
                            FROM drus_variables
                            WHERE name = @sensorName;";

                            await using(var cmd = new NpgsqlCommand(sql, dbConnection)) {
                                cmd.Parameters.AddWithValue("val", reading.Value);
                                cmd.Parameters.AddWithValue("ts", reading.Timestamp);
                                cmd.Parameters.AddWithValue("sensorName", reading.SensorId);

                                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                                if(rowsAffected == 0) {
                                    Console.WriteLine($"Error... Could not save log...");
                                } else {
                                    Console.WriteLine($"Log saved to DB.");
                                }
                            }
                            /*
                             Here I use sql query for alarms to prevent caching invalidation. Better solution would be making Rest API that would change max/min values in memory(that are saved in some dictionry from database(on startup of server)) AND in database at the same time, for sync purposes. TODO
                            Keeping this would make this server "stateless", which means there is nothing to care about when primary server crashes and 2ndary takes over since everything is taken care of in database(lower performance the higher the scale)
                             */
                            string alarmSqll = @"
                            INSERT INTO drus_alarms(var_id, alarm_type, severity, triggered_val, message)
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
                                    Console.WriteLine($" Alarm triggered for sensor {reading.SensorId} having value: {reading.Value}");
                                }
                            }
                        }
                    } catch(Exception ex) {
                        Console.WriteLine($"ERROR WHILE READING/DECRYPTING.... {ex.Message}");
                    }
                }
            } catch(Exception ex) {
                Console.WriteLine($"Connection lost: {ex.Message}");
            }
        }
        Console.WriteLine("Sensore disconnected...");
    }
}