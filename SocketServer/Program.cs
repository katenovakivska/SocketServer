using System.Net;
using System.Net.Sockets;

class Program
{
    //Steps to reproduce:
    //cd SocketServer
    //dotnet run -- 3
    //telnet 127.0.0.1 3

    //string parameter is client key, int parameter is sum
    static Dictionary<string, int> connectedClients = new Dictionary<string, int>();
    //several threads can try to change dictionary simultaneously so lock object
    //is needed to avoid race condition situation
    private static readonly object _dictionaryLock = new object();
    static async Task Main(string[] args)
    {
        //port value should be an integer value and it is restricted (should be more than 0 and less than 65536)
        if (args.Length < 1 ||
            !int.TryParse(args[0], out int port) ||
            port < 1 ||
            port > 65535)
        {
            Console.WriteLine("Enter command 'dotnet run <port>' where port is an integer value more than 0 and less than 65536");
            return;
        }

        //some IP address can be changed on other concrete address
        var ip = System.Net.IPAddress.Parse("127.0.0.1");
        var tspListener = new TcpListener(ip, port);
        tspListener.Start();
        Console.WriteLine($"Socket server is started on port: {port}");

        while (true)
        {
            TcpClient client = await tspListener.AcceptTcpClientAsync();
            //while local testing with telenet it's difficult to separate clients 'cause 
            //their IP-address are the same, so I decided to add port value for tracking 
            var clientKey = GetClientKey(client);
            lock (_dictionaryLock)
            {
                connectedClients[clientKey] = 0;
            }
            Console.WriteLine($"Client with IP:Port {GetClientKey(client)} is connected");
            _ = Task.Run(() => WorkWithClientAsync(client));
        }
    }

    static async Task WorkWithClientAsync(TcpClient client)
    {
        var clientKey = GetClientKey(client);
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            await writer.WriteLineAsync($"Welcome client with IP:Port {clientKey}");
            await writer.WriteLineAsync("Enter 'list' command or integer value");

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.Trim().ToLower() == "list")
                {
                    await writer.WriteLineAsync("List of connected clients:");
                    foreach (var connectedClient in connectedClients)
                    {
                        await writer.WriteLineAsync($"Client with IP:Port {connectedClient.Key} and sum {connectedClient.Value}");
                    }
                }
                else if (int.TryParse(line, out int number))
                {
                    Console.WriteLine($"Client with IP:Port {clientKey} entered value: {line}");
                    lock (_dictionaryLock)
                    {
                        connectedClients[clientKey] += number;
                    }
                    await writer.WriteLineAsync($"Computed sum: {connectedClients[clientKey]}");
                }
                else
                {
                    Console.WriteLine($"Client with IP:Port {clientKey} entered not correct value");
                    await writer.WriteLineAsync("Invalid input. Please enter a number or 'list'");
                }
            }
        }
        finally
        {
            client.Dispose();
            lock (_dictionaryLock)
            {
                connectedClients.Remove(clientKey);
            }
            Console.WriteLine($"Client with IP:Port {clientKey} is disconnected");
        }
    }

    private static string GetClientKey(TcpClient client)
    {
        var endPoint = (IPEndPoint?)client.Client.RemoteEndPoint;
        return endPoint != null ? $"{endPoint.Address}:{endPoint.Port}" : "empty";
    }
}