using System.Diagnostics;
using System.Reflection.PortableExecutable;

namespace Chatbox_Type_Shii
{
    public class Client
    {
        private readonly int port = 5000;
        private TcpClient? client;
        private CancellationTokenSource? cts;
        public string? filePath { get; private set; }
        public string? targetUsername { get; private set; }

        //Start The Client
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
                    Console.WriteLine("Connected to server...");
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

        //Handling Messages
        public async Task HandleMessages(TcpClient client, CancellationToken token)
        {
            Task recieve;
            Task send;
            {
                NetworkStream stream = client.GetStream();
                Console.Write("Please input your username: ");
                string? username = Console.ReadLine();
                if(username == null)
                    throw new ArgumentException("Username cannot be null."); username = username.Trim();
                ChatPackets packet = new ChatPackets();
                packet.Type = ChatPackets.PacketType.UserJoined;
                packet.Content = username;
                packet.Username = username;
                string jsonPacket = JsonSerializer.Serialize(packet);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPacket);
                byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
                await stream.WriteAsync(lengthPrefix);
                await stream.WriteAsync(jsonBytes);
                recieve = RecieveMessages(stream, token);
                send = SendMessages(username, stream, token);
                await Task.WhenAll(recieve, send);
            }
        }

        //Recieving Messages
        public async Task RecieveMessages( NetworkStream stream, CancellationToken token)
        {
            try
            {
                ChatPackets? packet = new ChatPackets();
                string? message;
                byte[] lengthBuffer = new byte[4];
                byte[] messageBuffer;
                while (!token.IsCancellationRequested)
                {
                    int v = await stream.ReadAsync(lengthBuffer, 0, lengthBuffer.Length, token);
                    int messageLength = BitConverter.ToInt32(lengthBuffer);
                    if (messageLength <= 0 || messageLength > 10_000_000)
                    {
                        throw new Exception("Invalid packet size.");
                    }
                    messageBuffer = new byte[messageLength];
                    await stream.ReadExactlyAsync(messageBuffer, token);
                    message = Encoding.UTF8.GetString(messageBuffer);
                    if (message == null)
                        break;
                    packet = JsonSerializer.Deserialize<ChatPackets>(message);
                    if (packet != null)
                    {
                        Console.WriteLine($"Received {packet.Type} from {packet.Username}");
                        switch (packet.Type)
                        {
                            case ChatPackets.PacketType.Message:
                                Console.WriteLine($"Message from {packet.Username}: {packet.Content}");
                                break;
                            case ChatPackets.PacketType.File:
                                Console.WriteLine($"File from {packet.Username}: {packet.FileName} to be saved at {packet.DestinationDirectory}");
                                string destinationDirectory = packet.DestinationDirectory ?? $@"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\Chatbox Files";
                                if (!Directory.Exists(destinationDirectory))
                                    Directory.CreateDirectory(destinationDirectory);
                                string destinationPath = Path.Combine(destinationDirectory, packet.FileName);
                                using (FileStream fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                                {
                                    byte[] buffer = new byte[81920];
                                    int bytesRead;
                                    long totalBytesRead = 0;
                                    long fileSize = (long)packet.FileSize;
                                    while (totalBytesRead < packet.FileSize && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                    {
                                        await fs.WriteAsync(buffer, 0, bytesRead);
                                        totalBytesRead += bytesRead;
                                        Console.Write($"\rProgress: {((double)totalBytesRead / fileSize):P2}");
                                    }
                                    Console.Write($"\nFile {packet.FileName} received successfully.");
                                }
                                break;
                            case ChatPackets.PacketType.UserList:
                                Console.WriteLine($"User list requested by {packet.Username}");
                                break;
                            case ChatPackets.PacketType.UserJoined:
                                Console.WriteLine($"{packet.Username} has joined the chat.");
                                break;
                            case ChatPackets.PacketType.UserLeft:
                                Console.WriteLine($"{packet.Username} has left the chat.");
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving messages: {ex.Message}");
            }
        }

        //Sending Messages
        public async Task SendMessages(string username, NetworkStream stream, CancellationToken token)
        {
            try
            {
                Console.WriteLine("You can start sending messages...");
                Console.WriteLine(@"use \msg to send a message and \file to send a file...");
                while (!token.IsCancellationRequested)
                {
                    string? input = Console.ReadLine();
                    if (input == null)
                        continue;
                    if (input.StartsWith(@"\msg"))
                    {
                        ChatPackets packet = new ChatPackets
                        {
                            Type = ChatPackets.PacketType.Message,
                            Username = username,
                            Content = input.Replace(@"\msg", "").Trim()
                        };
                        string jsonPacket = JsonSerializer.Serialize(packet);
                        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPacket);
                        byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
                        await stream.WriteAsync(lengthPrefix);
                        await stream.WriteAsync(jsonBytes);
                    }
                    if (input.StartsWith(@"\file"))
                    {
                        do
                        {
                            Console.Write("Enter the file path: ");
                            filePath = Console.ReadLine();
                            Console.Write("Enter the target username: ");
                            targetUsername = Console.ReadLine();
                        } while (File.Exists(filePath) == false || string.IsNullOrEmpty(targetUsername));
                        FileInfo fileInfo = new FileInfo(filePath);
                        ChatPackets packets = new ChatPackets
                        {
                            Type = ChatPackets.PacketType.File,
                            Username = username,
                            FileName = fileInfo.Name,
                            FileExtension = fileInfo.Extension.TrimStart('.'),
                            FileSize = (long)fileInfo.Length,
                            TargetUsername = targetUsername
                        };
                        string jsonPacket = JsonSerializer.Serialize(packets);
                        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPacket);
                        byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
                        await stream.WriteAsync(lengthPrefix);
                        await stream.WriteAsync(jsonBytes);
                        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            byte[] buffer = new byte[81920];
                            int bytesRead;
                            long totalBytesRead = 0;
                            long fileSize = packets.FileSize;
                            while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await stream.WriteAsync(buffer, 0, bytesRead);
                                totalBytesRead += bytesRead;
                                Console.Write($"\rProgress: {((double)totalBytesRead / fileSize):P2}");
                            }
                            Console.WriteLine($"\nFile {packets.FileName} sent successfully.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending messages: {ex.Message}");
            }
        }
    }
}