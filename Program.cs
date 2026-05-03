using Chatbox_type_shii;
using Chhatbox_type_shii;
using System;
using System.IO;
using System.Net;
using System.Text;

Console.WriteLine("1. Start Server");
Console.WriteLine("2. Start Client");
Console.Write("Choose: ");

string? choice = Console.ReadLine();

if (choice == "1")
{
    Console.Write("Input server IP: ");
    string? input = Console.ReadLine();
    Server server = new Server();
    await server.StartServer();
}
else if (choice == "2")
{
    Client client = new Client();

    Console.Write("Enter IP: ");
    string? ip = Console.ReadLine();

    client.Connect(ip, 5000).GetAwaiter().GetResult();
}