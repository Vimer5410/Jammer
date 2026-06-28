using System.Net;
using System.Net.Sockets;
using System.Text;
using Jammer.Core;

class Program
{
    public static string serverIp { get; private set; }
    
    public static int serverPort { get; private set; }
    private static string? userName { get; set; }

    private static Socket tcpSocket;

    private static byte[] key = new byte[32];
    
    async static Task Main(string[] args)
    {
        var rand = new Random();
        Console.WriteLine("Введите ip сервера:");
        serverIp = Console.ReadLine() switch { "" or null => "127.0.0.1", string s => s };
        Console.WriteLine("Введите порт для TCP соединения:");
        serverPort = Convert.ToInt32(Console.ReadLine() switch { "" or null => "7777", string s => s });
        Console.WriteLine("Введите ваше имя:");
        userName = Console.ReadLine() switch{"" or null => $"User {rand.Next(1000, 9999)}", string s => s};

        await CreateTcpConnection();
        
        await Task.WhenAll(ReceiveMessageAsync(),SendMessageAsync());
        
    }

    async static Task CreateTcpConnection()
    {
        tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
        
        try
        {
            await tcpSocket.ConnectAsync(serverEndPoint);
            key = await CreateAesKey();
            Console.WriteLine("==========TCP соедение установлено=======");
        }
        catch (Exception ex)
        { 
            Console.WriteLine($"Ошибка: {ex.Message}");
            Environment.Exit(1);
        }
    }

    async static Task ReceiveMessageAsync()
    {
        while (true)
        {
            byte[] buffer = await Frame.ReadFrameAsync(tcpSocket);
            
            var message = Encoding.UTF8.GetString(Crypto.AES.Decrypt(buffer, key));
            Console.WriteLine($"{message}");
        }
    }

    async static Task SendMessageAsync()
    {
        
        while (true)
        {
            var input = await Task.Run(()=> Console.ReadLine());
            if (string.IsNullOrEmpty(input)) continue;
            
            string fullMessage = $"[{userName}]: {input}";
            var data = Crypto.AES.Encrypt(fullMessage, key);
            if (input == "/exit")
            {
                tcpSocket.Shutdown(SocketShutdown.Both);
                tcpSocket.Close();
                Environment.Exit(0);
            } 
            
            await Frame.WriteFrameAsync(tcpSocket,data);
        }
    }
    
    
    async static Task<byte[]> CreateAesKey()
    {
        Crypto.ECDH ecdh = new Crypto.ECDH();
        await ecdh.SendLocalPublickeyAsync(tcpSocket);
        var serverPublicKey = await ecdh.ReceiveRemotePublicKeyAsync(tcpSocket);
        var aesKey= ecdh.CreateSecret(serverPublicKey);
        return aesKey;
    }
}
