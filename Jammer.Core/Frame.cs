using System.Buffers.Binary;
using System.Data.Common;
using System.Net.Sockets;

namespace Jammer.Core;

public class Frame
{

    public static byte[] WriteFrame(byte[] data)
    {
        byte[] frame = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(frame, data.Length);
        byte[] alldata = frame.Concat(data).ToArray();
        return alldata;
    }

    public static async Task<byte[]> ReadFrameAsync(Socket client)
    {
        var firstBytes =await ReadExactlyAsync(client, 4);
        var allMessageLength = BinaryPrimitives.ReadInt32BigEndian(firstBytes);

        var data =await ReadExactlyAsync(client, allMessageLength);
        return data;
    }

    public static async Task<byte[]> ReadExactlyAsync(Socket socket, int byteCount)
    {
        byte[] buffer = new byte[byteCount];
        int total=0;
        while (total<byteCount)
        {
            int bytesRead = await socket.ReceiveAsync(buffer.AsMemory(total, byteCount-total));
            total += bytesRead;
        }
        
        return buffer;
    }
}