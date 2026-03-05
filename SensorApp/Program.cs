using System.Net.Sockets;
using System.Text.Json;
using SharedUtils;
using SharedUtils.DTOs;

namespace SensorApp;

class Program {
    static async Task Main(string[] args) {
        string serverIp = Environment.GetEnvironmentVariable("SERVER_HOST") ?? "127.0.0.1";
        int port = 8080;
        string mySensorId = Environment.GetEnvironmentVariable("SENSOR_ID") ?? "01";

        Console.WriteLine($"{mySensorId} starting up....");

        try {
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(serverIp, port);
            Console.WriteLine($"Connected to server at {serverIp}:{port}");

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
                Console.WriteLine("Sent Pubblic Key to Server!");

                string? responseJson = await reader.ReadLineAsync();
                var response = JsonSerializer.Deserialize<HandshakeResponseDto>(responseJson!);

                if(response == null || !response.IsAccepted) {
                    Console.WriteLine("Error: Server rejected the handshake");
                    return;
                }

                mySharedSecret = myDh.DeriveSharedSecret(response.PublicKey);

                string secretPreview = BitConverter.ToString(mySharedSecret).Substring(0, 14);
                Console.WriteLine($"Handshake succsefull! My Shared Secret starts with: {secretPreview}"); // This is just for preview, never do this in prod!!!   
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
                Console.WriteLine($"generated: {jsonMessage}");
                Console.WriteLine($"sent encrypted: {encryptedMessage}");

                await Task.Delay(2000);
            }
        } catch(Exception ex) {
            Console.WriteLine($"Error, could not connect to server: {ex.Message}");
        }
    }
}