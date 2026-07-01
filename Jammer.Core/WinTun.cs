using System.Runtime.InteropServices;

namespace Jammer.Core;

public class WinTun
{
    [DllImport("wintun.dll")]
    static extern uint WintunGetRunningDriverVersion();

    public static void WinTunTest()
    {
        try
        {
            uint version = WintunGetRunningDriverVersion();
            Console.WriteLine($"Wintun version: {version >> 16}.{version & 0xFFFF}");
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("DLL не найдена(указан не тот путь или разрядность)");
        }
        catch (EntryPointNotFoundException)
        {
            Console.WriteLine("DLL найдена, но функция не та - возможно неверная версия Wintun");
        }
    }

    // работа с адаптером
    [DllImport("wintun.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr WintunCreateAdapter
    (
        string name,
        string tunnelType,
        IntPtr requestedGUID
    );

    [DllImport("wintun.dll")]
    public static extern void WintunCloseAdapter
    (
        IntPtr adapter
    );

    
    // работа с сессией
    [DllImport("wintun.dll")]
    public static extern IntPtr WintunStartSession
    (
        IntPtr adapter,
        uint capacity
    );

    [DllImport("wintun.dll")]
    public static extern void WintunEndSession
    (
        IntPtr session
    );

    
    // работа с сетью и пакетами
    [DllImport("wintun.dll")]
    public static extern void WintunSendPacket
    (
        IntPtr session,
        IntPtr packet
    );

    [DllImport("wintun.dll")]
    public static extern IntPtr WintunReceivePacket
    (
        IntPtr session,
        out uint packetSize
    );

    [DllImport("wintun.dll")]
    public static extern void WintunReleaseReceivePacket
    (
        IntPtr session,
        IntPtr packet
    );

    [DllImport("wintun.dll")]
    public static extern IntPtr WintunAllocateSendPacket
    (
        IntPtr session,
        uint packetSize
    );
}