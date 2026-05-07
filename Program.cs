using Chatbox_Type_Shii;

int choice = 0;
string? input = null;
bool isValid;

Console.WriteLine("Welcome to Horizon MVP version 1.1");
Console.WriteLine("1.Server\n2.Client");
do
{
    try
    {
        Console.Write("Kindly select what you would like to run as: ");
        input = Console.ReadLine();
        isValid = Int32.TryParse(input, out choice);
        if (choice != 1 && choice != 2)
        {
            Console.WriteLine("Invalid choice. Please select either 1 for Server or 2 for Client.");
        }
    }
    catch
    {
        Console.WriteLine("Invalid input. Please enter a valid number.");
        return;
    }
} while (choice < 1 || choice > 2 || !isValid);
switch (choice)
{
    case 1:
        Server server = new Server();
        Console.WriteLine("Please input your desired IP to host server, may leave blank for default: ");
        input = Console.ReadLine();
        await server.StartServer(input);
        break;
    case 2:
        Client client = new Client();
        Console.WriteLine("Please input the server IP to connect to: ");
        input = Console.ReadLine();
        string? inpute = input?.Trim();
        await client.StartClient(inpute);
        break;
}