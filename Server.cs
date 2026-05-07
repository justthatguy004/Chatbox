global using System;
global using System.Text;
global using System.Threading.Tasks;
global using System.Net;
global using System.IO;
global using System.Collections.Concurrent;
global using System.Net.Sockets;
global using System.Text.Json;
using System.Runtime.CompilerServices;

namespace Chatbox_Type_Shii
{
    public class Server
    {
        private readonly int port = 5000;
        private TcpListener? listener;
        private CancellationTokenSource? cts;
        private ConcurrentDictionary<string, TcpClient> clients = new ConcurrentDictionary<string, TcpClient>();
        public async Task StartServer(string? userIP)
        {
            if (cts != null)
            {
                throw new InvalidOperationException("Server is already running...");
            }
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress? hostIP;
            try
            {
                if (IPAddress.TryParse(userIP, out var parsed))
                {
                    hostIP = parsed;
                }
                else
                {
                    hostIP = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                }
                if (hostIP == null)
                {
                    Console.WriteLine("No suitable IP adress found...");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting host IP: {ex.Message}");
                return;
            }
            listener = new TcpListener(hostIP, port);
            listener.Start();
            await Task.Run(() => RunServerAsync(listener, token), token);
        }
        private async Task RunServerAsync(TcpListener listener, CancellationToken token)
        {
            Console.WriteLine($"Server started on {listener.LocalEndpoint}");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client, token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }
        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream))
            using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
            {
                Task recieve = RecieveMessagesAsync(client, reader, stream, token);
                Task send = SendMessagesAsync(client, writer, token);
                await Task.WhenAny(recieve, send);
            }
        }
        private async Task RecieveMessagesAsync(TcpClient client, StreamReader reader, NetworkStream stream, CancellationToken token)
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tempfile.dat");
                string fileName = Path.GetFileName(filePath);
                if (File.Exists(filePath))
                {
                    string baseName = Path.GetFileNameWithoutExtension(filePath);
                    string ext = Path.GetExtension(filePath);
                    filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                    int counter = 1;

                    while (File.Exists(filePath))
                    {
                        filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                            $"{baseName} (Copy{counter}){ext}");
                        counter++;
                    }
                }
                string? clientUsername = reader.ReadLine();
                if (clientUsername == null) return;
                string trueUserName = clientUsername.Replace("USERNAME: ", "");
                clients[trueUserName] = client;
                Console.WriteLine($"{trueUserName} has connected.");
                while (!token.IsCancellationRequested)
                {
                    byte[] buffer = new byte[81920];
                    Array.Clear(buffer, 0, buffer.Length);
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string jsonMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    ChatPackets? packet = JsonSerializer.Deserialize<ChatPackets>(jsonMessage);
                    if (packet == null) break;
                    if (packet.Type == ChatPackets.PacketType.Message)
                    {
                        BroadcastMessage(packet.Content ?? string.Empty, client);
                        Console.WriteLine($"{clients.First(wag => wag.Value == client).Key}: {packet.Content}");
                    }
                    if (packet.Type == ChatPackets.PacketType.File)
                    {
                        BroadcastMessage($"Sent a file: {packet.FileName}", client);
                        FileInfo sourceFile = new FileInfo(filePath);
                        byte[] fileBytes = File.ReadAllBytes(filePath);
                        int bytesSent = 0;
                        long progress = 0;
                        long totalBytes = sourceFile.Length;
                        while ((bytesSent = await stream.ReadAsync(fileBytes, 0, fileBytes.Length, token)) > 0)
                        {
                            await stream.WriteAsync(fileBytes, 0, bytesSent, token);
                            progress += bytesSent;
                            Console.Write($"\rProgress: {(double)progress / totalBytes:P2}");
                        }
                        Console.WriteLine("\nFile copied Successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
            }
        }
        private async Task SendMessagesAsync(TcpClient client, StreamWriter writer, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    string? message = await Task.Run(() => Console.ReadLine());
                    if (message == null) break;
                    string? serverMessage = $"SERVER: {message}";
                    foreach (var kvp in clients)
                    {
                        using (NetworkStream stream = kvp.Value.GetStream())
                        {
                            writer.WriteLine(serverMessage);
                        }
                    }
                           
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }
        private void BroadcastMessage(string message, TcpClient sender)
        {
            string senderUsername = clients.First(wag => wag.Value == sender).Key;
            foreach (var kvp in clients)
            {
                TcpClient client = kvp.Value;
                if (client != sender)
                {
                    try
                    {
                        using (NetworkStream stream = client.GetStream())
                        using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
                        {
                            writer.WriteLine($"{senderUsername}: {message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error broadcasting to {kvp.Key}: {ex.Message}");
                    }
                }
            }
        }
    } 
}