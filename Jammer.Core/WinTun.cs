using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Jammer.Core;

public class WinTun : ITun
{
    [DllImport("wintun.dll")]
    static extern uint WintunGetRunningDriverVersion();

    private static readonly Guid _guid = new Guid("1d453f1f-f44a-4aec-bbba-199313aa96ec");
    
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

    public void InitializeTunnel()
    {
        
        IntPtr requestedGUID = Marshal.AllocHGlobal(Marshal.SizeOf(_guid));
        
        try
        {
            Marshal.StructureToPtr(_guid, requestedGUID, false);
            WintunCloseAdapter(_tunAdapter);
            
            _tunAdapter = WintunCreateAdapter("JammerTun", "Jammer", requestedGUID);
            
            if (_tunAdapter == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();

                if (errorCode==183)
                {
                    Console.WriteLine("[WinTun] адаптер уже существует в системе, переподключаемся...");
                    _tunAdapter = WintunOpenAdapter("JammerTun");
                }
                
                if (errorCode == 5)
                {
                    throw new UnauthorizedAccessException("[WinTun] запустите приложение от имени администратора!");
                }

                if (_tunAdapter == IntPtr.Zero)
                {
                    throw new InvalidProgramException($"[WinTun] не удалось создать виртуальный адаптер, код ошибки {errorCode}");
                }
            }
            else
            {
                Console.WriteLine("[WinTun] адаптер успешно создан");
            }
            
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
    public void ConfigureIpAddress(string ipAddress, int maskLength)
    {
        //вычисляем маску подсети по ее префиксу типа /24
        string mask;
        if (maskLength==0)
        {
            mask="0.0.0.0";
        }
        else
        {
            uint maskUint=(0xFFFFFFFF<<(32-maskLength));
            uint b1 = maskUint >> 24;
            uint b2 = (maskUint >> 16) & 0xFF;
            uint b3 = (maskUint >> 8) & 0xFF;
            uint b4 = maskUint & 0xFF;
            mask = $"{b1}.{b2}.{b3}.{b4}";
        }
        
        
        ProcessStartInfo processStartInfo = new ProcessStartInfo();
        processStartInfo.FileName = "netsh";
        processStartInfo.Arguments = $"interface ipv4 set address name=\"JammerTun\" source=static addr={ipAddress} mask={mask} gateway=none";
        processStartInfo.CreateNoWindow = true;
        processStartInfo.Verb = "runas";
        processStartInfo.UseShellExecute = false;
    
        //добавляем логирование ошибок
        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.RedirectStandardError = true;

        Task.Delay(1000);
        using (Process process = Process.Start(processStartInfo))
        {
            if (process==null)
            {
                throw new InvalidOperationException("[WinTun] Не удалалось запустить процесс netsh");
            }
            
            process.WaitForExit();
            
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
            
            Console.WriteLine($"[WinTun] ipAddress успешно задан для виртуального адаптера");
        }

    }

/// <summary>
/// Запуск сессии чтения/записи пакетов
/// </summary>

    public void StartSession()
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
    public void SendPacket(byte[] packet)
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
    
    public byte[] ReceivePacket()
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