using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Chhatbox_type_shii
{
    public class Client
    {
        private readonly CancellationTokenSource cts;
        private TcpClient client;
        private readonly CancellationToken token;
        public Client()
        {
            client = new TcpClient();
            cts = new CancellationTokenSource();
            token = cts.Token;
        }
        public async Task Connect(string? inputIP, int port)
        {
            if (token.IsCancellationRequested)
            {
                Console.WriteLine("Exiting...");
                return;
            }
            try
            {
                IPAddress ip;
                if (IPAddress.TryParse(inputIP, out var parsed))
                {
                    ip = parsed;
                }
                else
                {
                    Console.WriteLine("Invalid IP address...");
                    return;
                }
                IPEndPoint ipEndPoint = new IPEndPoint(ip, port);
                await client.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);
                Console.WriteLine("Connected to the server...");
                using (var Stream = client.GetStream())
                using (StreamReader reader = new StreamReader(Stream, Encoding.UTF8))
                using (StreamWriter writer = new StreamWriter(Stream, Encoding.UTF8) { AutoFlush = true })
                {
                    Console.Write("Enter your username: ");
                    var message = Console.ReadLine();
                    await writer.WriteLineAsync($"USERNAME:{message}");
                    Task reading = ListenForMessages(reader, token);
                        Task writing = SendMessages(writer, token);
                        await Task.WhenAll(reading, writing);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        public async Task ListenForMessages(StreamReader reader, CancellationToken token)
        {
                while (!token.IsCancellationRequested)
                {
                    var message = await reader.ReadLineAsync();
                    if (message == null)
                    {
                        Console.WriteLine("Server disconnected.");
                        Disconnect();
                        break;
                    }
                    Console.WriteLine($"Received: {message}");
                }
        }
        public async Task SendMessages(StreamWriter writer, CancellationToken token)
        {
                while (!token.IsCancellationRequested)
                {
                    var message = Console.ReadLine();
                    if (message == null) continue;
                    await writer.WriteLineAsync(message);
                }
        }
        public void Disconnect()
        {
            cts.Cancel();
            client.Close();
            Console.WriteLine("Disconnected from the server...");
        }
    }
}
