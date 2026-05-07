using System.Diagnostics;
using System.Reflection.PortableExecutable;

namespace Chatbox_Type_Shii
{
    public class Client
    {
        private readonly int port = 5000;
        private TcpClient? client;
        private CancellationTokenSource? cts;

        public async Task StartClient(string? IP)
        {
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            client = new TcpClient();
            try
            {
                if (IPAddress.TryParse(IP, out var parsed))
                {
                    client.ConnectAsync(parsed, port).Wait(token);
                }
                else
                {
                    throw new ArgumentException("Invalid IP address.");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to server: {ex.Message}");
            }
            await Task.Run(() => HandleMessages(client, token), token);
        }
        public async Task HandleMessages(TcpClient client, CancellationToken token)
        {
            {
                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream);
                StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };
                Task recieve = RecieveMessages(client, reader, stream, token);
                Task send = SendMessages(client, writer, stream, token);
                await Task.WhenAll(recieve, send);
            }
        }
        public async Task SendMessages(TcpClient client, StreamWriter writer, NetworkStream stream, CancellationToken token)
        {
            string? userName;
            string? input;
            string? user;
                Console.Write("Please enter your desired username: ");
                input = Console.ReadLine();
                while (true)
                {
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        Console.Write("Username cannot be empty. Please enter a valid username: ");
                    }
                    else
                    {
                        userName = $"USERNAME: {input}";
                        user = userName.Replace("USERNAME: ", "");
                        await writer.WriteLineAsync(userName);
                        break;
                    }
                }
            Console.WriteLine($"Connected to server as {user}.");
            Console.WriteLine(@"Please input \msg for messages and \file for file sharing before typing your message.");
            while (!token.IsCancellationRequested)
            {
                ChatPackets packet = new ChatPackets();
                Console.Write("Enter message: ");
                string? message = Console.ReadLine();
                if (message == null) break;
                if (message.StartsWith(@"\msg"))
                {
                    packet.Type = ChatPackets.PacketType.Message;
                    packet.Username = user;
                    packet.Content = message.Replace(@"\msg", "").Trim();
                    string? json = JsonSerializer.Serialize(packet);
                    await writer.WriteAsync(json);
                    Console.WriteLine($"{user}: {message.Replace(@"\msg", "").Trim()}");
                }
                else if (message.StartsWith(@"\file"))
                {
                    Console.Write("Please input the file path: ");
                    string? filePath = Console.ReadLine();
                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine("File not found.");
                        continue;
                    }
                    Console.Write("\nPlease input the destination directory: ");
                    string? destinationDirectory = Console.ReadLine();
                    packet.Type = ChatPackets.PacketType.File;
                    packet.Username = userName;
                    packet.Content = filePath;
                    packet.DestinationDirectory = destinationDirectory;
                    packet.FileName = Path.GetFileName(filePath);
                    FileInfo sourceFile = new FileInfo(filePath);
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    byte[] buffer = new byte[81920];
                    int bytesSent = 0;
                    long progress = 0;
                    long totalBytes = sourceFile.Length;
                    while ((bytesSent = await stream.ReadAsync(fileBytes, 0, buffer.Length, token)) > 0)
                    {
                        await stream.WriteAsync(buffer, 0, bytesSent, token);
                        progress += bytesSent;
                        Console.Write($"\rProgress: {(double)progress / totalBytes:P2}");
                    }
                    Console.WriteLine("\nFile copied Successfully");
                }

            }

        }
        public async Task RecieveMessages(TcpClient client, StreamReader reader, NetworkStream stream, CancellationToken token)
        {
            
        }
    }
}