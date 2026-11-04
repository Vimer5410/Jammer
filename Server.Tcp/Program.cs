using System.Net;
using System.Net.Sockets;
using System.Text;
using Jammer.Core;
using Serilog;
using Serilog.Core;
using Jammer.Core.Tun;

class Program
{
    public static int localPort { get; private set; }

    private static Socket tcpSocket;
    
    private static ITun _tun;

    private static ILogger _serverTcpLogger = Log.ForContext("SourceContext", "Server-Tcp");

    private static HashSet<int> clientIds = new HashSet<int>();

    private static TunDispatcher _tunDispatcher;
    async static Task Main(string[] args)
    {
        Logging.ConfigureLogger();
        
        _serverTcpLogger.Information("Введите порт для TCP соединения:");
        localPort = Convert.ToInt32(Console.ReadLine() switch{"" or null => "7777", string s => s}) ;

        _tun = ITun.CreateTun();
        
        _tun.InitializeTunnel();
        
        _tun.StartSession();
        
        _tun.ConfigureIpAddress("172.16.0.1", 24);
        
        _tunDispatcher = new TunDispatcher(_tun);
        
        _tunDispatcher.Start();
        
        await CreateTcpConnection();

        while (true)
        {
            Socket client = await tcpSocket.AcceptAsync();
            _ = HandleClientAsync(client);
            _serverTcpLogger.Information("клиент принят: {Client}", client.RemoteEndPoint);
            
        }
        
    }
    
    async static Task HandleClientAsync(Socket client)
    {
        int clientId = 0;
        ClientSession clientSession = null;
        try
        {
            Crypto.ECDH ecdh = new Crypto.ECDH();
            byte[] key = new byte[32];
            key= await ecdh.CreateAesKey(client);
                
            clientId = GenerateUniqueClientsIdentifier();
            byte[] clientIdBytes = BitConverter.GetBytes(clientId);
            await SendClientIdAsync(client, clientIdBytes, key);

            clientSession = new ClientSession {Key = key, Socket = client};
            _tunDispatcher.Add(clientId, clientSession);
            
            await Task.WhenAny(ReceiveMessageAsync(client, key), SendMessageAsync(clientSession));
            
        }
        catch (Exception ex)
        {
            _serverTcpLogger.Information("клиент отключился: {Ex}",ex.Message);
        }
        finally
        {
            _tunDispatcher.Remove(clientId);
            clientIds.Remove(clientId);
            client.Close();
            clientSession?.Queue.Writer.Complete();
            _serverTcpLogger.Information("{id} удален", clientId);
        }
    }
    
    static int GenerateUniqueClientsIdentifier()
    {
        
        Random random = new Random();
        
        var clientId = random.Next(2, 255);
        if (clientIds.Contains(clientId))
        {
            clientId = GenerateUniqueClientsIdentifier();
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
    async static Task ReceiveMessageAsync(Socket client, byte[] key)
    {
        while (client.Connected)
        {
            byte[] buffer = await Frame.ReadFrameAsync(client);

            var data = Crypto.AES.Decrypt(buffer, key);
            _tun.SendPacket(data);
            
            _serverTcpLogger.Debug("получено {Bytes} байт", data.Length);
        }
    }
    

    async static Task SendMessageAsync(ClientSession clientSession)
    {
        var client = clientSession.Socket;
        var key = clientSession.Key;
        var queue= clientSession.Queue;
        
        
        await foreach (var el in queue.Reader.ReadAllAsync())
        {
            byte[] data = Crypto.AES.Encrypt(el, key);
            await Frame.WriteFrameAsync(client, data);
        }
        
    }
    
    async static Task SendClientIdAsync(Socket client, byte[] input, byte[] key)
    {
        if (client.Connected)
        {
            var data = Crypto.AES.Encrypt(input, key);
            await Frame.WriteFrameAsync(client, data);
        }
    }
    
}

