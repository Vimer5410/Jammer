
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client;

class Program
{
    static async Task Main(string[] args)
    {
        byte[] buffer = new byte[120];
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var ip = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1666);
        await socket.ConnectAsync(ip);


        var sendTask = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var data = Console.ReadLine();
                    await socket.SendAsync(Encoding.UTF8.GetBytes(data));
                }
            
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        });

        var receiveTask = Task.Run(async () =>
        {
            while (true)
            {
                int buffersize = await socket.ReceiveAsync(buffer);
                var message = Encoding.UTF8.GetString(buffer, 0, buffersize);
                Console.WriteLine($"New message:{message}");
            }
        });


        await Task.WhenAll(sendTask, receiveTask);

    }
}