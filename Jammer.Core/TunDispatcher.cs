using System.Collections.Concurrent;
using System.Net.Sockets;
using Serilog;

namespace Jammer.Core;

public class ClientSession
{
    public Socket Socket;
    public byte[] Key;
}

//читаем пакет смотрим кому он и отправляем этому клиенту
public class TunDispatcher
{
    private static ILogger _logger = Log.ForContext("SourceContext", "TunDispatcher");

    private readonly ITun _tun;

    
    private readonly ConcurrentDictionary<int, ClientSession> _clients = new();

    public TunDispatcher(ITun tun)
    {
        _tun = tun;
    }

    public void Add(int clientId, ClientSession session)
    {
        _clients[clientId] = session;
    }

    public void Remove(int clientId)
    {
        _clients.TryRemove(clientId, out _);
    }

    public void Start()
    {
        new Thread(ReadLoop)
        {
            IsBackground = true
        }.Start();
    }

    private void ReadLoop()
    {
        while (true)
        {
            try
            {
                byte[] packet = _tun.ReceivePacket();

                // TODO: проверить, что пакет достаточно длинный и это IPv4
                int clientId = packet[19]; //последний байт адреса назначения 172.16.0.id

                if (!_clients.TryGetValue(clientId, out var client)) continue; 

                byte[] data = Crypto.AES.Encrypt(packet, client.Key);
                Frame.WriteFrameAsync(client.Socket, data).Wait();
            }
            catch (Exception ex)
            {
                _logger.Error("ошибка: {Ex}", ex.Message);
            }
        }
    }
}
