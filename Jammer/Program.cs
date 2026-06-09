using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace Server;

class Program
{
    public static readonly string localIpAddress = "127.0.0.1";
    public static int localPort { get; set; }
    
    public static int port { get; set; }
    
    async static Task Main(string[] args)
    {
        Console.WriteLine("Введите порт для приема сообщений:");
        localPort = Convert.ToInt32(Console.ReadLine());
        Console.WriteLine("Введите порт клиента(для отправки сообщений):");
        port = Convert.ToInt32(Console.ReadLine());
        await Task.WhenAll(SendMessageAsync(), ReceiveMessageAsync());
    }

    async static Task SendMessageAsync()
    {
        var sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        while (true)
        {
            
            string input= await Task.Run(() => Console.ReadLine());
            if (string.IsNullOrEmpty(input)) continue;
            
            var data = Encoding.UTF8.GetBytes(input);
            
            await sendSocket.SendToAsync(data, new IPEndPoint(IPAddress.Parse(localIpAddress), port));
        }
    }

    async static Task ReceiveMessageAsync()
    {
        var receiveSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        receiveSocket.Bind(new IPEndPoint(IPAddress.Parse(localIpAddress), localPort));
        byte[] buffer = new byte[256];
        
        while (true)
        {
            var data = await receiveSocket.ReceiveFromAsync(buffer, new IPEndPoint(IPAddress.Any, 0));
            var message = Encoding.UTF8.GetString(buffer, 0, data.ReceivedBytes);
            Console.WriteLine($"New message:{message}");
        }
    }
}