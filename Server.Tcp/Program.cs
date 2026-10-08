using System.Net;
using System.Net.Sockets;
using System.Text;
using Jammer.Core;
using Serilog;
using Serilog.Core;

class Program
{
    public static int localPort { get; private set; }

    private static Socket tcpSocket;

    private static byte[] key = new byte[32];

    private static ITun _tun;

    private static ILogger _serverTcpLogger = Log.ForContext("SourceContext", "Server-Tcp");
    static async Task Main(string[] args)
    {
        Logging.ConfigureLogger();
        
        _serverTcpLogger.Information("Введите порт для TCP соединения:");
        localPort = Convert.ToInt32(Console.ReadLine() switch{"" or null => "7777", string s => s}) ;

        _tun = ITun.CreateTun();
        
        _tun.InitializeTunnel();
        
        _tun.StartSession();
        
        _tun.ConfigureIpAddress("172.16.0.1", 24);
        
        await CreateTcpConnection();

        while (true)
        {
            Socket client = await tcpSocket.AcceptAsync();
            _serverTcpLogger.Debug("клиент принят: {Client}", client.RemoteEndPoint);
            
            try
            {
                //вычисляем AES ключ по общему секрету
                Crypto.ECDH ecdh = new Crypto.ECDH();
                key = await ecdh.CreateAesKey(client);
                
                await Task.WhenAll(ReceiveMessageAsync(client), SendMessageAsync(client));
            }
            catch (Exception ex)
            {
                _serverTcpLogger.Debug("клиент отключился: {Ex}",ex.Message);
            }
        }
        
    }

    async static Task CreateTcpConnection()
    {
        tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var serverEndPoint = new IPEndPoint(IPAddress.Any, localPort);
        
        try
        {
            tcpSocket.Bind(serverEndPoint);
            tcpSocket.Listen();
            _serverTcpLogger.Information("==========TCP соедение установлено=======");
        }
        catch (Exception ex)
        {
            _serverTcpLogger.Error("ошибка: {Ex}", ex.Message);
            Environment.Exit(1);
        }
    }
    async static Task ReceiveMessageAsync(Socket client)
    {
        while (client.Connected)
        {
            byte[] buffer = await Frame.ReadFrameAsync(client);

            var data = Crypto.AES.Decrypt(buffer, key);
            _tun.SendPacket(data);
            
            _serverTcpLogger.Debug("получено {Bytes} байт", data.Length);
        }
    }

    async static Task SendMessageAsync(Socket client)
    {
        while (client.Connected)
        {
            var input = _tun.ReceivePacket();
            if (input==null) continue;
            
            var data = Crypto.AES.Encrypt(input, key);
            
            await Frame.WriteFrameAsync(client, data);
        }
    }
    
}

