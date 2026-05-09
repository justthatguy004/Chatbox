global using System;
global using System.Text;
global using System.Threading.Tasks;
global using System.Net;
global using System.IO;
global using System.Collections.Concurrent;
global using System.Net.Sockets;
global using System.Text.Json;

namespace Chatbox_Type_Shii
{
    public class Server
    {
        private int port = 5000;
        private TcpListener? listener;
        private CancellationTokenSource? cts;
        private ConcurrentDictionary<string, TcpClient> clients = new ConcurrentDictionary<string, TcpClient>();
        private IPAddress? hostIp;

        //Start The Server
        public async Task StartServer()
        {
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            hostIp = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (hostIp == null)
                throw new Exception("No IPv4 address found for the host.");
            IPEndPoint localEndPoint = new IPEndPoint(hostIp, port);
            listener = new TcpListener(localEndPoint);
            listener.Start();
            Console.WriteLine($"Server started on {hostIp}:{port}");
            await Task.Run(() => AcceptClientAsync(listener, token), token);
        }

        //Accepting Clients
        public async Task AcceptClientAsync(TcpListener listener, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        //Handling Clients
        public async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream);
                StreamWriter writer = new StreamWriter(stream) { AutoFlush = true };
                /* string? imessage;
                imessage = await reader.ReadLineAsync();
                if (imessage == null)
                    throw new Exception("Username cannot be null, exiting...");
                string? username = imessage.Replace("USERNAME: ", ""); 
                clients.TryAdd(username, client); */
                Task recieve;
                Task send;
                while (!token.IsCancellationRequested)
                {
                    recieve = RecieveMessages(client, reader, stream, token);
                    send = SendMessages(token);
                    await Task.WhenAny(recieve, send);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
        }

        //Recieving Messages
        public async Task RecieveMessages(TcpClient client, StreamReader reader, NetworkStream stream, CancellationToken token)
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
                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    if (messageLength <= 0 || messageLength > 10_000_000)
                    {
                        throw new Exception("Invalid packet size.");
                    }
                    messageBuffer = new byte[messageLength];
                    await stream.ReadExactlyAsync(messageBuffer, 0, messageBuffer.Length, token);
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
                                await BroadcastMessageAsync(client, packet, token);
                                break;
                            case ChatPackets.PacketType.File:
                                packet.DestinationDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), packet.FileName);
                                Console.WriteLine($"File from {packet.Username}: {packet.FileName} to be saved at {packet.DestinationDirectory}");
                                TcpClient? tcpClient = clients.TryGetValue(packet.TargetUsername, out TcpClient? targetclient) ? targetclient : null;
                                if(targetclient == null)
                                    break;
                                NetworkStream targetStream = targetclient.GetStream();
                                string? json = JsonSerializer.Serialize(packet);
                                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
                                byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
                                await targetStream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length, token);
                                await targetStream.WriteAsync(jsonBytes, 0, jsonBytes.Length, token);
                                    int amountRecieved = 0;
                                    byte[] buffer = new byte[81920];
                                    while (amountRecieved < packet.FileSize)
                                    {
                                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                                        if (bytesRead == 0)
                                            break;
                                        await targetStream.WriteAsync(buffer, 0, bytesRead, token);
                                        amountRecieved += bytesRead;
                                    }
                                break;
                            case ChatPackets.PacketType.UserList:
                                Console.WriteLine($"User list requested by {packet.Username}");
                                break;
                            case ChatPackets.PacketType.UserJoined:
                                Console.WriteLine($"{packet.Username} has joined the chat.");
                                ChatPackets packets = new ChatPackets
                                {
                                    Type = ChatPackets.PacketType.Message,
                                    Username = "SERVER",
                                    Content = $"{packet.Username} has joined the chat."
                                };
                                await BroadcastMessageAsync(packets, token);
                                if (packet.Username == null)
                                    throw new Exception("Username cannot be null, exiting...");
                                clients.TryAdd(packet.Username, client);
                                break;
                            case ChatPackets.PacketType.UserLeft:
                                Console.WriteLine($"{packet.Username} has left the chat.");
                                if(packet.Username == null)
                                    throw new Exception("Username cannot be null, exiting...");
                                clients.TryRemove(packet.Username, out _);
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

        //Broadcasting Messages
        public async Task BroadcastMessageAsync(ChatPackets packet, CancellationToken token)
        {
            string? jsonPacket = JsonSerializer.Serialize(packet);
            foreach (var kvp in clients)
            {
                TcpClient client = kvp.Value;
                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPacket);
                    byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
                    await stream.WriteAsync(lengthPrefix);
                    await stream.WriteAsync(jsonBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting to {kvp.Key}: {ex.Message}");
                }
            }

        }
        public async Task BroadcastMessageAsync(TcpClient sender, ChatPackets packet, CancellationToken token)
        {
            string? jsonPacket = JsonSerializer.Serialize(packet);
            foreach (var kvp in clients)
            {
                TcpClient client = kvp.Value;
                if (client == sender)
                    continue;
                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPacket);
                    byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
                    await stream.WriteAsync(lengthPrefix);
                    await stream.WriteAsync(jsonBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting to {kvp.Key}: {ex.Message}");
                }
            }

        }

        //Sending Messages
        public async Task SendMessages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                string? input = Console.ReadLine();
                string? message = $"SERVER: {input}";
                ChatPackets packet = new ChatPackets
                {
                    Type = ChatPackets.PacketType.Message,
                    Username = "SERVER",
                    Content = message
                };
                await BroadcastMessageAsync(packet, token);
            }
        }
    }
}