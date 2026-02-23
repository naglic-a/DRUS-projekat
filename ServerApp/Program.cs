using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using SharedUtils.DTOs;
using Serilog;

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
        using (client)
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new StreamReader(stream)) {
            try {
                string? line;
                while((line = await reader.ReadLineAsync()) != null) {
                    var reading = JsonSerializer.Deserialize<SensorReadingDto>(line);

                    if(reading != null) {
                        Console.WriteLine($"Received reading: {reading.SensorId}, Temp: {reading.Value} C");
                    }
                }
            } catch(Exception ex) {
                Console.WriteLine($"Connection lost: {ex.Message}");
            }
        }

        Console.WriteLine("Sensore disconnected...");
    }
}