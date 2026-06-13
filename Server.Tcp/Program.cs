using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    public static readonly string localIpAddress = "127.0.0.1";
    public static int localPort { get; private set; }

    private static Socket tcpSocket;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Введите порт для TCP соединения:");
        localPort = Convert.ToInt32(Console.ReadLine() switch{"" or null => "7777", string s => s}) ;

        await CreateTcpConnection();
        
        Socket client = await tcpSocket.AcceptAsync();
        
        await Task.WhenAll(ReceiveMessageAsync(client), SendMessageAsync(client));
    }

    async static Task CreateTcpConnection()
    {
        tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var serverEndPoint = new IPEndPoint(IPAddress.Parse(localIpAddress), localPort);
        
        try
        {
            tcpSocket.Bind(serverEndPoint);
            tcpSocket.Listen();
            Console.WriteLine("==========TCP соедение установлено=======");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
            Environment.Exit(1);
        }
    }
    async static Task ReceiveMessageAsync(Socket client)
    {
        byte[] buffer = new byte[120];
        
        while (true)
        {
            int buffersize = await client.ReceiveAsync(buffer);
            var message = Encoding.UTF8.GetString(buffer, 0, buffersize);
            Console.WriteLine($"{message}");
        }
    }

    async static Task SendMessageAsync(Socket client)
    {
        
        while (true)
        {
            string data = await Task.Run(() => Console.ReadLine());
            
            if (string.IsNullOrEmpty(data)) continue;
            
            await client.SendAsync(Encoding.UTF8.GetBytes("[Server]: " + data));
        }
    }
}

