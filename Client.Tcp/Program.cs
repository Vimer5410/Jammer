using System.Net;
using System.Net.Sockets;
using System.Text;
using Jammer.Core;
using Serilog;

class Program
{
    public static string serverIp { get; private set; }
    
    public static int serverPort { get; private set; }

    private static Socket tcpSocket;

    private static byte[] key = new byte[32];

    private static ITun _tun;

    private static IRoute _route;

    private static ILogger _clientTcpLogger = Log.ForContext("SourceContext", "Client-Tcp");

    static List<int> clientIds = new List<int>();
    
    async static Task Main(string[] args)
    {
        Logging.ConfigureLogger();
        _route = IRoute.CreateRoute();
        
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
        { 
            _route.Clean(serverIp, "JammerTun");
        };

        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            _route.Clean(serverIp, "JammerTun");
            Environment.Exit(0);
        };
        
        
        _clientTcpLogger.Information("Введите ip сервера:");
        serverIp = Console.ReadLine() switch { "" or null => "77.221.140.145", string s => s };
        _clientTcpLogger.Information("Введите порт для TCP соединения:");
        serverPort = Convert.ToInt32(Console.ReadLine() switch { "" or null => "7777", string s => s });

        string tunnelIp = $"172.16.0.{Generate()}";
        
        _tun = ITun.CreateTun();
        
        _tun.InitializeTunnel();
        
        _tun.StartSession();
        
        _tun.ConfigureIpAddress(tunnelIp, 24);
        
        _route.Route(serverIp, tunnelIp,null, null);
        
        _route.DNS();
        
        //ping 172.16.0.1 -l 1000
        await CreateTcpConnection();
        await Task.WhenAll(ReceiveMessageAsync(), SendMessageAsync());
        
    }

    static int Generate()
    {
        
        Random random = new Random();
        
        var clientId = random.Next(2, 255);
        if (clientIds.Contains(clientId))
        {
            clientId = Generate();
        }
        else
        {
            clientIds.Add(clientId);
        }
        
        return clientId;
    }
    
    async static Task CreateTcpConnection()
    {
        tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        tcpSocket.NoDelay = true;
        var serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
        
        try
        {
            await tcpSocket.ConnectAsync(serverEndPoint);
            _clientTcpLogger.Information("==========TCP соедение установлено=======");
            
            //вычисляем AES ключ по общему секрету
            Crypto.ECDH ecdh = new Crypto.ECDH();
            key = await ecdh.CreateAesKey(tcpSocket);
        }
        catch (Exception ex)
        { 
            _clientTcpLogger.Error("Ошибка: {Ex}", ex.Message);
            Environment.Exit(1);
        }
    }

    async static Task ReceiveMessageAsync()
    {
        while (true)
        {
            byte[] buffer = await Frame.ReadFrameAsync(tcpSocket);
            
            var data = Crypto.AES.Decrypt(buffer, key);
            _tun.SendPacket(data);
            
            _clientTcpLogger.Debug("Получено {Bytes} байт", data.Length);
        }
    }

    async static Task SendMessageAsync()
    {
        
        while (true)
        {
            var input = _tun.ReceivePacket();
            
            //fix: пофиксить загрузку одного ядра в 100% через WintunGetReadWaitEvent (if (input == null) continue бесконечно по кругу крутиться и забивает весь поток)
            if (input == null) continue;
            
            var data = Crypto.AES.Encrypt(input, key);
            await Frame.WriteFrameAsync(tcpSocket,data);
            
            _clientTcpLogger.Debug("Отправлено {Bytes} байт", data.Length);
        }
    }
    
}
