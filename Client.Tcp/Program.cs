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

    private static IntPtr _tunAdapter = IntPtr.Zero; 
    async static Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
        { 
            Routing.Clean(serverIp, "JammerTun");
        };

        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Routing.Clean(serverIp,"JammerTun");
            Environment.Exit(0);
        };
        
        var rand = new Random();
        Console.WriteLine("Введите ip сервера:");
        serverIp = Console.ReadLine() switch { "" or null => "192.168.31.176", string s => s };
        Console.WriteLine("Введите порт для TCP соединения:");
        serverPort = Convert.ToInt32(Console.ReadLine() switch { "" or null => "7777", string s => s });
        Console.WriteLine("Введите ваше имя:");
        userName = Console.ReadLine() switch{"" or null => $"User {rand.Next(1000, 9999)}", string s => s};
        
        _tunAdapter=WinTun.InitializeTunnel();
        Console.WriteLine($"!!! {_tunAdapter}");
        
        WinTun.StartSession();
        
        await WinTun.ConfigureIpAddress("172.16.0.2", "255.255.255.0");

         // await Task.Delay(3000);
        
         await Routing.Route(serverIp, null, null);
         await Routing.DNS();
        
        //ping 172.16.0.1 -l 1000
        await CreateTcpConnection();
        await Task.WhenAll(ReceiveMessageAsync(), SendMessageAsync());
        
    }
    
    async static Task CreateTcpConnection()
    {
        tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        tcpSocket.NoDelay = true;
        var serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
        
        try
        {
            await tcpSocket.ConnectAsync(serverEndPoint);
            Console.WriteLine("==========TCP соедение установлено=======");
            
            //вычисляем AES ключ по общему секрету
            Crypto.ECDH ecdh = new Crypto.ECDH();
            key = await ecdh.CreateAesKey(tcpSocket);
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
            
            var data = Crypto.AES.Decrypt(buffer, key);
            WinTun.SendPacket(data);
            Console.WriteLine($"Получено {data.Length} байт");
        }
    }

    async static Task SendMessageAsync()
    {
        
        while (true)
        {
            var input = WinTun.ReceivePacket();
            
            //fix: пофиксить загрузку одного ядра в 100% через WintunGetReadWaitEvent (if (input == null) continue бесконечно по кругу крутиться и забивает весь поток)
            if (input == null) continue;
            
            var data = Crypto.AES.Encrypt(input, key);
            await Frame.WriteFrameAsync(tcpSocket,data);
            
            Console.WriteLine($"Отправлено {data.Length} байт");
        }
    }
    
}
