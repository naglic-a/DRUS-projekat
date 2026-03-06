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
            Random rand = new Random();

            while(true) {
                var reading = new SensorReadingDto {
                    SensorId = mySensorId,
                    Value = (decimal)(rand.NextDouble() * (85.9 - 20.0) + 20.0),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                string jsonMessage = JsonSerializer.Serialize(reading);

                string encryptedMessage = AesEncryptionHelper.Encrypt(jsonMessage, mySharedSecret);

                await writer.WriteLineAsync(encryptedMessage);
                Log.Debug("Generated reading: {Reading}", jsonMessage);
                Log.Debug("Sent encrypted message: {EncryptedLength} bytes", encryptedMessage.Length);

                await Task.Delay(2000);
            }
        } catch(Exception ex) {
            Log.Error(ex, "Error occurred while connecting to server");
        } finally {
            Log.CloseAndFlush();
        }
    }
}