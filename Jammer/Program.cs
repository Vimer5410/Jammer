using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server;

class Program
{
    static async Task Main(string[] args)
    {
        
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        byte[] buffer = new byte[120];
        var serverip = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1666);
        socket.Bind(serverip);
        socket.Listen();
        Socket receive = await socket.AcceptAsync();


        

        var receiveTask = Task.Run(async () =>
        {
            while (true)
            {
                int buffersize= await receive.ReceiveAsync(buffer);
                var message = Encoding.UTF8.GetString(buffer, 0, buffersize);
                Console.WriteLine($"New message:{message}");
            }
        }); 

        var sendTask = Task.Run(async () =>
        {
            var data = Console.ReadLine();
            await receive.SendAsync(Encoding.UTF8.GetBytes(data));
        });


        await Task.WhenAll(receiveTask, sendTask);
        
    }
}