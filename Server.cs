using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Collections.Concurrent;

namespace Chatbox_type_shii
{
    public class Server
    {
        private int port = 5000;
        public static ConcurrentDictionary<string, TcpClient> ConnectedClients = new();
        public int Port
        {
            get { return port; }
            set { port = value; }
        }
        private TcpListener? listener;
        private CancellationTokenSource? cts;
        public async Task StartServer(string? inputIP = null)
        {
            if (cts != null)
                throw new InvalidOperationException("Server is already running...");
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            await Task.Run(() => RunServerAsync(inputIP, token), token);
        }
        public void StopServer(string? inputIP)
        {
            try
            {
                cts?.Cancel();
                listener?.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Problem stopping server: {ex.Message}");
            }
            finally
            {
                cts?.Dispose();
                cts = null;
            }
        }
        public static void AddClient(string? usernname, TcpClient client) => ConnectedClients.TryAdd(usernname ?? Guid.NewGuid().ToString().PadLeft(5, '0'), client);
        public async Task RunServerAsync(string? inputIP, CancellationToken token)
        {
            try
            {
                ConnectedClients.Clear();
                IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress? ipAddress;
                if (!token.IsCancellationRequested && IPAddress.TryParse(inputIP, out var parsed))
                {
                    ipAddress = parsed;
                }
                else
                {
                    ipAddress = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                }
                if (ipAddress == null)
                {
                    Console.WriteLine("No valid IP address found...");
                    return;
                }
                IPEndPoint hostEndPoint = new IPEndPoint(ipAddress, Port);
                listener = new TcpListener(hostEndPoint);
                listener.Start();
        Console.WriteLine($"Server started on {hostEndPoint}...");
                while (!token.IsCancellationRequested)
                {
                    Console.WriteLine("Waiting for connection...");
                    TcpClient client;
                    try
                    {
                        client = await listener.AcceptTcpClientAsync();
                        Console.WriteLine("Client connected");
                        await Task.Run(() => HandleClientAsync(client, token), token);
                    }
                    catch (SocketException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping Server: {ex.Message}");
            }
        }
        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                    using (client)
                    using (NetworkStream stream = client.GetStream())
                    using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string? usernameMessage = await reader.ReadLineAsync();
                        if (usernameMessage == null) return;
                        string username = usernameMessage.Replace("USERNAME:", "").Trim();
                        ConnectedClients.TryAdd(username, client);
                        Console.WriteLine($"{username} joined");
                        Task receiveTask = ReceiveMessages(reader, username, token);
                        Task sendTask = SendMessages(writer, token);
                        await Task.WhenAny(receiveTask, sendTask);
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
        }
        private static async Task ReceiveMessages(StreamReader reader, string username, CancellationToken token)
        {
            string? message;

            while (!token.IsCancellationRequested &&
                   (message = await reader.ReadLineAsync()) != null)
            {
                Console.WriteLine($"{username}: {message}");

                Broadcast(message, username);
            }

            ConnectedClients.TryRemove(username, out _);
        }
        private static async Task SendMessages(StreamWriter writer, CancellationToken token)
        {
            string? message;
            while (!token.IsCancellationRequested)
            {
                message = Console.ReadLine();
                if (message == null) continue;
                await writer.WriteLineAsync(message);
            }
        }
        private static void Broadcast(string message, string sender)
        {
            foreach (var client in ConnectedClients)
            {
                if (client.Key == sender) continue;

                try
                {
                    var stream = client.Value.GetStream();
                    var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                    writer.WriteLine($"{sender}: {message}");
                }
                catch
                {
                    // ignore dead clients for now
                }
            }
        }
    }
}