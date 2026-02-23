using System.Net.Sockets;
using System.Text.Json;
using SharedUtils.DTOs;

namespace SensorApp;

class Program {
    static async Task Main(string[] args) {
        string serverIp = "127.0.0.1"; // nekako Preko env varijabli?
        int port = 8080;
        string mySensorId = "1423"; // nekako Preko env varijabli?

        Console.WriteLine($"{mySensorId} starting up....");

        try {
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(serverIp, port);
            Console.WriteLine($"Connected to server at {serverIp}:{port}");

            using NetworkStream stream = client.GetStream();
            using StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };

            Random rand = new Random();

            while(true) {
                var reading = new SensorReadingDto {
                    SensorId = mySensorId,
                    Value = decimal(rnd.NextDouble() * (85.9 - 20.0) + 20.0),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                string jsonMessage = JsonSerializer.Serialize(reading);

                await writer.WriteLineAsync(jsonMessage);
                Console.WriteLine($"sent {jsonMessage}");

                await Task.Delay(2000);
            }
        } catch(Exception ex) {
            Console.WriteLine($"Error, could not connect to server: {ex.Message}");
        }
    }
}