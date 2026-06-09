
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace Client;

class Program
{
    private static string serverIp { get; set; }
    private static int serverPort { get; set; }
    private static int clientPort { get; set; }
    private static string? userName { get; set; }
    static async Task Main(string[] args)
    {
        Console.WriteLine("Введите ip сервера:");
        serverIp = Console.ReadLine() switch { "" or null => "127.0.0.1", string s => s };
        Console.WriteLine("Введите порт сервера:");
        serverPort = Convert.ToInt32(Console.ReadLine() switch { "" or null => "7777", string s => s });
        Console.WriteLine("Введите ваш порт:");
        clientPort = Convert.ToInt32(Console.ReadLine() switch{"" or null => "8888", string s=>s});
        Console.WriteLine("Введите ваше имя:");
        
        string hostName = Dns.GetHostName();
        IPAddress[] addresses = Dns.GetHostAddresses(hostName);
        
        AddressFamily myIp = AddressFamily.Unspecified;
        
        foreach (var ip in addresses)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                Console.WriteLine(ip);
            }   
        }
        
        userName = Console.ReadLine();
        
        var sendTask = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    string input = await Task.Run(() => Console.ReadLine());
                    if (string.IsNullOrEmpty(input)) continue;
                    var data = Encoding.UTF8.GetBytes($"[{userName}] "+input);
                    var sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    await sendSocket.SendToAsync(data, new IPEndPoint(IPAddress.Parse(serverIp), serverPort));
                }
            
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        });

        var receiveTask = Task.Run(async () =>
        {
            var receiveSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            receiveSocket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), clientPort));
            byte[] buffer = new byte[256];
            while (true)
            {
                var data = await receiveSocket.ReceiveFromAsync(buffer, new IPEndPoint(IPAddress.Any, clientPort));
                var message = Encoding.UTF8.GetString(buffer, 0, data.ReceivedBytes);
                Console.WriteLine($"New message:{message}");
            }
        });


        await Task.WhenAll(sendTask, receiveTask);

    }
}