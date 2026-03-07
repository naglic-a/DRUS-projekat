using System.Net.Sockets;
using System.Text.Json;
using SharedUtils;
using SharedUtils.DTOs;
using Serilog;

namespace SensorApp;

class Program {
    static async Task Main(string[] args) {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/sensor-.txt", 
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        bool isMySensorActive = Environment.GetEnvironmentVariable("START_ACTIVE") != "false";

        try {
            string serverIp = Environment.GetEnvironmentVariable("SERVER_HOST") ?? "127.0.0.1";
            int port = 8080;
            string mySensorId = Environment.GetEnvironmentVariable("SENSOR_ID") ?? "01";

            Log.Information("Sensor {SensorId} starting up", mySensorId);

            using TcpClient client = new TcpClient();
            await client.ConnectAsync(serverIp, port);
            Log.Information("Connected to server at {ServerIp}:{Port}", serverIp, port);

            using NetworkStream stream = client.GetStream();
            using StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };
            using StreamReader reader = new StreamReader(stream);

            byte[] mySharedSecret;
            // Diffie-Hellman handshake
            using(var myDh = new DhKeyExchange()) {
                var request = new HandshakeRequestDto {

                    SensorId = mySensorId,
                    PublicKey = myDh.PublicKey
                };
                await writer.WriteLineAsync(JsonSerializer.Serialize(request));
                Log.Debug("Sent public key to server");

                string? responseJson = await reader.ReadLineAsync();
                var response = JsonSerializer.Deserialize<HandshakeResponseDto>(responseJson!);

                if(response == null || !response.IsAccepted) {
                    Log.Error("Server rejected the handshake");
                    return;
                }

                mySharedSecret = myDh.DeriveSharedSecret(response.PublicKey);

                string secretPreview = BitConverter.ToString(mySharedSecret).Substring(0, 14);
                Log.Warning("Handshake successful! Shared secret starts with: {SecretPreview} (DO NOT LOG IN PRODUCTION!)", secretPreview);
            }

            _ = Task.Run(async () => { 
                while(true) {
                    try {
                        string? encryptedCmd = await reader.ReadLineAsync();
                        if(encryptedCmd != null) {
                            string cmd = AesEncryptionHelper.Decrypt(encryptedCmd, mySharedSecret);

                            if(cmd == "BECOME_ACTIVE") {
                                Log.Information("Got command from server: Switching to Active mode");
                                isMySensorActive = true;
                            } else if(cmd == "BECOME_STANDBY") {
                                Log.Information("Got command from server: Switching to Standby mode");
                                isMySensorActive = false;
                            }
                        }
                    } catch {
                        await Task.Delay(1000); 
                    }
                }
            });
            Random rand = new Random();

            SemaphoreSlim networkLock = new SemaphoreSlim(1, 1);
            _ = Task.Run(async () => { 
                while(true) {
                    await Task.Delay(10000);

                    var heartbeat = new SensorReadingDto {
                        SensorId = mySensorId,
                        IsHeartbeat = true
                    };

                    string jsonMessage = JsonSerializer.Serialize(heartbeat);
                    string encryptedMessage = AesEncryptionHelper.Encrypt(jsonMessage, mySharedSecret);

                    await networkLock.WaitAsync();
                    try {
                        await writer.WriteLineAsync(encryptedMessage);
                    } finally {
                        networkLock.Release();
                    }

                    Log.Information("Sent heartbeat message to server");
                }
            });

            while(true) {
                if(!isMySensorActive) {
                    await Task.Delay(1000);
                    continue;
                }

                var reading = new SensorReadingDto {
                    SensorId = mySensorId,
                    Value = (decimal)(rand.NextDouble() * (85.9 - 20.0) + 20.0),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    IsHeartbeat = false
                };

                string jsonMessage = JsonSerializer.Serialize(reading);
                string encryptedMessage = AesEncryptionHelper.Encrypt(jsonMessage, mySharedSecret);

                await networkLock.WaitAsync();
                try {
                    await writer.WriteLineAsync(encryptedMessage);
                } finally {                     
                    networkLock.Release();
                }

                Log.Debug("Generated reading: {Reading}", jsonMessage);
                Log.Debug("Sent encrypted message: {Message}, {EncryptedLength} bytes ", encryptedMessage.Length, encryptedMessage);

                await Task.Delay(rand.Next(1000, 20000));
            }
        } catch(Exception ex) {
            Log.Error(ex, "Error occurred while connecting to server");
        } finally {
            Log.CloseAndFlush();
        }
    }
}