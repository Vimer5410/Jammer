using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Jammer.Core;

public class WinTun
{
    [DllImport("wintun.dll")]
    static extern uint WintunGetRunningDriverVersion();

    private static readonly Guid _guid = new Guid("12345678-1234-1234-1234-123456789abc");

    private static IntPtr _tunAdapter= IntPtr.Zero;
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
    [DllImport("wintun.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr WintunCreateAdapter
    (
        string name,
        string tunnelType,
        IntPtr requestedGUID
    );

    [DllImport("wintun.dll")]
    private static extern void WintunCloseAdapter
    (
        IntPtr adapter
    );

    [DllImport("wintun.dll")]
    public static extern IntPtr WintunOpenAdapter
    (
        string name
    );
    
    // работа с сессией
    [DllImport("wintun.dll")]
    private static extern IntPtr WintunStartSession
    (
        IntPtr adapter,
        uint capacity
    );

    [DllImport("wintun.dll")]
    private static extern void WintunEndSession
    (
        IntPtr session
    );

    
    // работа с сетью и пакетами
    [DllImport("wintun.dll")]
    private static extern void WintunSendPacket
    (
        IntPtr session,
        IntPtr packet
    );

    [DllImport("wintun.dll")]
    private static extern IntPtr WintunReceivePacket
    (
        IntPtr session,
        out uint packetSize
    );

    [DllImport("wintun.dll")]
    private static extern void WintunReleaseReceivePacket
    (
        IntPtr session,
        IntPtr packet
    );

    [DllImport("wintun.dll")]
    private static extern IntPtr WintunAllocateSendPacket
    (
        IntPtr session,
        uint packetSize
    );


    public static IntPtr InitializeTunnel()
    {
        IntPtr requestedGUID = Marshal.AllocHGlobal(Marshal.SizeOf(_guid));

        try
        {
            Marshal.StructureToPtr(_guid, requestedGUID, false);
            _tunAdapter = WintunCreateAdapter("JammerTun", "Jammer", requestedGUID);
            
            if (_tunAdapter == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidProgramException($"[WinTun] не удалось создать виртуальный адаптер, код ошибки {errorCode}");
            }
            else
            {
                Console.WriteLine("[WinTun] адаптер успешно создан");
            }

            return _tunAdapter;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WinTun] {ex}");
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(requestedGUID);
        }
    }

    public static async void ConfigureIpAddress(string ipAddress, string mask)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo();
        processStartInfo.FileName = "netsh";
        processStartInfo.Arguments = $"interface ipv4 set address name=\"JammerTun\" source=static addr={ipAddress} mask={mask} gateway=none";
        processStartInfo.CreateNoWindow = true;
        processStartInfo.Verb = "runas";
        processStartInfo.UseShellExecute = true;

        await Task.Delay(1000);
        using (Process process = Process.Start(processStartInfo))
        {
            if (process==null)
            {
                throw new InvalidOperationException("[WinTun] Не удалалось запустить процесс netsh");
            }

            process.WaitForExit();

            if (process.ExitCode!=0)
            {
                throw new InvalidOperationException($"netsh завершился с ошибкой. Код: {process.ExitCode}");
            }

            Console.WriteLine("[WinTun] ipAddress успешно задан для виртуального адаптера");
        }

    }
    
}