using System.Net;
using System.Net.Sockets;
using System.Text;
using Jammer.Core;

class Program
{
    public static readonly string localIpAddress = "127.0.0.1";
    public static int localPort { get; private set; }

    private static Socket tcpSocket;

    private static byte[] key = new byte[32];

    static async Task Main(string[] args)
    {
        Console.WriteLine("Введите порт для TCP соединения:");
        localPort = Convert.ToInt32(Console.ReadLine() switch{"" or null => "7777", string s => s}) ;
        await CreateTcpConnection();

        while (true)
        {
            Socket client = await tcpSocket.AcceptAsync();
            try
            {
                //вычисляем AES ключ по общему секрету
                Crypto.ECDH ecdh = new Crypto.ECDH();
                key = await ecdh.CreateAesKey(client, true);
                
                await Task.WhenAll(ReceiveMessageAsync(client), SendMessageAsync(client));
            }
            catch (Exception ex)
            {
                Console.WriteLine("клиент отключился: " + ex.Message);
            }
        }
        
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
        while (true)
        {
            byte[] buffer = await Frame.ReadFrameAsync(client);
            
            var message = Encoding.UTF8.GetString(Crypto.AES.Decrypt(buffer, key));
            Console.WriteLine($"{message}");
        }
    }

    async static Task SendMessageAsync(Socket client)
    {
        while (true)
        {
            string input = await Task.Run(() => Console.ReadLine());
            if (string.IsNullOrEmpty(input)) continue;
            
            string fullMessage = $"[Server]: {input}";
            var data = Crypto.AES.Encrypt(fullMessage, key);
            
            await Frame.WriteFrameAsync(client, data);
        }
    }
    
}

