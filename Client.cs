using System.Diagnostics;
using System.Reflection.PortableExecutable;

namespace Chatbox_Type_Shii
{
    public class Client
    {
        private readonly int port = 5000;
        private TcpClient? client;
        private CancellationTokenSource? cts;

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
            {
                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream);
                StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };
                Console.Write("Please input your username: ");
                string? username = Console.ReadLine();
                if(username == null)
                    throw new ArgumentException("Username cannot be null."); username = username.Trim();
                ChatPackets packet = new ChatPackets();
                packet.Type = ChatPackets.PacketType.UserJoined;
                packet.Content = username;
                packet.Username = username;
                string jsonPacket = JsonSerializer.Serialize(packet);
                await writer.WriteLineAsync(jsonPacket);
                Task recieve = RecieveMessages(reader, stream, token);
                Task send = SendMessages(username, writer, stream, token);
                await Task.WhenAll(recieve, send);
            }
        }

        //Recieving Messages
        public async Task RecieveMessages(StreamReader reader, NetworkStream stream, CancellationToken token)
        {
            try
            {
                ChatPackets? packet = new ChatPackets();
                string? message;
                while (!token.IsCancellationRequested)
                {
                    message = await reader.ReadLineAsync();
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
        public async Task SendMessages(string username, StreamWriter writer, NetworkStream stream, CancellationToken token)
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
                    if (input.StartsWith("\\msg"))
                    {
                        ChatPackets packet = new ChatPackets
                        {
                            Type = ChatPackets.PacketType.Message,
                            Username = username,
                            Content = input
                        };
                        string jsonPacket = JsonSerializer.Serialize(packet);
                        await writer.WriteLineAsync(jsonPacket);
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