using System.Net;
using System.Net.Sockets;
using System.Text;
using Jammer.Core;

class Program
{
    public static readonly string localIpAddress = "192.168.31.166";
    public static int localPort { get; private set; }

    private static Socket tcpSocket;

    private static byte[] key = new byte[32];

    private static IntPtr _tunAdapter = IntPtr.Zero; 
    static async Task Main(string[] args)
    {
        Console.WriteLine("Введите порт для TCP соединения:");
        localPort = Convert.ToInt32(Console.ReadLine() switch{"" or null => "7777", string s => s}) ;
        
        _tunAdapter=WinTun.InitializeTunnel();
        Console.WriteLine($"!!! {_tunAdapter}");
        
        WinTun.StartSession();
        await WinTun.ConfigureIpAddress("172.16.0.1", "255.255.255.0");
        
        await CreateTcpConnection();

        while (true)
        {
            Socket client = await tcpSocket.AcceptAsync();
            Console.WriteLine("[Server] клиент принят: " + client.RemoteEndPoint);
            
            try
            {
                //вычисляем AES ключ по общему секрету
                Crypto.ECDH ecdh = new Crypto.ECDH();
                key = await ecdh.CreateAesKey(client, true);
                
                await Task.WhenAll(ReceiveMessageAsync(client), SendMessageAsync(client));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Server] клиент отключился: " + ex.Message);
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
            Console.WriteLine("==========TCP соедение установлено=======");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] ошибка: {ex.Message}");
            Environment.Exit(1);
        }
    }
    async static Task ReceiveMessageAsync(Socket client)
    {
        while (true)
        {
            byte[] buffer = await Frame.ReadFrameAsync(client);

            var data = Crypto.AES.Decrypt(buffer, key);
            WinTun.SendPacket(data);
            Console.WriteLine($"[Server] получено {data.Length} байт");
        }
    }

    async static Task SendMessageAsync(Socket client)
    {
        while (true)
        {
            var input = WinTun.ReceivePacket();
            if (input==null) continue;
            
            var data = Crypto.AES.Encrypt(input, key);
            
            await Frame.WriteFrameAsync(client, data);
        }
    }
    
}

