using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Jammer.Core;

public class WinTun
{
    [DllImport("wintun.dll")]
    static extern uint WintunGetRunningDriverVersion();

    private static readonly Guid _guid = new Guid("12345678-1234-1234-1234-123456789abc");

    private static IntPtr _tunAdapter= IntPtr.Zero;
    
    private static IntPtr _session = IntPtr.Zero;
    
    private static IntPtr  _receivedPackets;
    
    private const uint _capacity = 0x2000000;      /* 32мб */

    private static uint _packetSize = 0xFFFF;
        
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
    [DllImport("wintun.dll", CharSet = CharSet.Unicode, SetLastError = true)]
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

    [DllImport("wintun.dll", CharSet = CharSet.Unicode, SetLastError = true)]
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


    /// <summary>
    /// инициализация WinTun интерфейса
    /// </summary>

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

    /// <summary>
    /// Привязка метаданных к WinTun интерфейсу
    /// </summary>
    public static async Task ConfigureIpAddress(string ipAddress, string mask)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo();
        processStartInfo.FileName = "netsh";
        processStartInfo.Arguments = $"interface ipv4 set address name=\"JammerTun\" source=static addr={ipAddress} mask={mask} gateway=none";
        processStartInfo.CreateNoWindow = true;
        processStartInfo.Verb = "runas";
        processStartInfo.UseShellExecute = false;
    
        //добавляем логирование ошибок
        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.RedirectStandardError = true;

        await Task.Delay(1000);
        using (Process process = Process.Start(processStartInfo))
        {
            if (process==null)
            {
                throw new InvalidOperationException("[WinTun] Не удалалось запустить процесс netsh");
            }

            process.WaitForExit();

            //fix: код ошибки 183, при каждом втором перезапуске почему то выкидывает 183,
            //будто адаптер уже создан, однако в диспетчере устройств его нет
        
            // читаем и выводим полный лог ошибки вместо старого "код ошибки 183....."
            string error = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"[netsh stdError] {error}");
            }
            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine($"[netsh stdOut] {output}");
            }

            if (process.ExitCode!=0)
            {
                throw new InvalidOperationException($"netsh завершился с ошибкой. Код: {process.ExitCode}");
            }

            Console.WriteLine("[WinTun] ipAddress успешно задан для виртуального адаптера");
        }

    }

/// <summary>
/// Запуск сессии чтения/записи пакетов
/// </summary>

    public static IntPtr StartSession()
    {
        try
        {
            _session = WintunStartSession(_tunAdapter, _capacity);

            if (_session == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidProgramException($"[WinTun] не удалось открыть сессию, код ошибки: {errorCode}");
            }
            else
            {
                Console.WriteLine("[WinTun] сессия успешно создана");
            }

            return _session;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WinTun] {ex}");
            throw;
        }
        
    }

    /// <summary>
    /// Отправка IP-пакета из OS
    /// </summary>
    public static void SendPacket(byte[] packet)
    {
        if (_session == IntPtr.Zero)
        {
            throw new InvalidOperationException("[WinTun] сессия не активна");
        }

        int packetLength = packet.Length;
        IntPtr buffer = WintunAllocateSendPacket(_session, (uint)packetLength);

        if (buffer == IntPtr.Zero)
        {
            throw new OutOfMemoryException("[WinTun] не удалось выделить память под пакет");
        }
        
        Marshal.Copy(packet, 0, buffer, packetLength);
        
        WintunSendPacket(_session, buffer);
    }

    /// <summary>
    /// Чтение IP-пакета из OS (исходящий трафик из windows в ваш туннель)
    /// </summary>
    
    public static byte[] ReceivePacket()
    {
        if (_session == IntPtr.Zero)
        {
            throw new InvalidOperationException("[WinTun] сессия не активна");
        }
    
        _receivedPackets = WintunReceivePacket(_session, out _packetSize);
    
        if (_receivedPackets == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            return null;
        }
    
        byte[] receivedPacketsBytes = new byte[_packetSize];
        Marshal.Copy(_receivedPackets, receivedPacketsBytes, 0, (int)_packetSize);
        WintunReleaseReceivePacket(_session, _receivedPackets);
        
        if (receivedPacketsBytes!=null)
        {
            Console.WriteLine($"[WinTun] получено {receivedPacketsBytes.Length} байт");
        }

        return receivedPacketsBytes;
    }
}