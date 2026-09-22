using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Jammer.Core;

public class LinuxTun
{
    private const int O_RDWR = 2;
    
    private const short IFF_TUN = 0x0001;

    private const short IFF_NO_PI = 0x1000;
    
    //номер команды ioctl складывается из четырёх частей (так устроен макрос _IOW в ядре Linux)
    // чтобы каждая часть встала на своё место в итоговом числе её умножают на своё число:
    private const ulong IOC_DIR_PLACE = 0x40000000;  // множитель для направления
    private const ulong IOC_SIZE_PLACE = 0x10000;    // множитель для размера данных
    private const ulong IOC_TYPE_PLACE = 0x100;      // множитель для буквы драйвера
    //номер команды стоит в самом конце числа, его умножать не нужно

    private const ulong IOC_WRITE = 1;
    
    //драйвер 'T' (TUN), команда номер 202, передаём данные размером с int
    private static readonly ulong TUNSETIFF = CalcIoctlNumber(IOC_WRITE, sizeof(int), 'T', 202);
    
    // направление * IOC_DIR_PLACE + размер * IOC_SIZE_PLACE + буква * IOC_TYPE_PLACE + номер
    private static ulong CalcIoctlNumber(ulong direction, ulong size, char type, ulong number)
    {
        return direction * IOC_DIR_PLACE
               + size * IOC_SIZE_PLACE
               + type * IOC_TYPE_PLACE
               + number;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct Ifreq
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ifrn_name;  //= ifr_ifrn union, 16 байт

        public short ifr_flags; //= первые 2 байта ifr_ifru union
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        public byte[] padding;  //= оставшиеся 22 байта union
        
        public Ifreq(string adapterName, short flag, byte[] pd)
        {
            byte[] adapterNameBytes = Encoding.UTF8.GetBytes(adapterName);

            if (adapterNameBytes.Length>15)
            {
                throw new ArgumentException("[LinuxTun] имя интерфейса слишком длинное(должно быть менее 16 байт)");
            }

            ifrn_name = new byte[16];
            adapterNameBytes.CopyTo(ifrn_name, 0);

            ifr_flags = flag;
            padding = pd;
        }
    }
    
    [DllImport("libc", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int ioctl
    (
        int fd,
        ulong op,
        ref Ifreq ifreq
    );
    
    [DllImport("libc", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int close
    (
        int fd
    );
    
    [DllImport("libc", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int open
    (
        string pathname,
        int flags
    );
    
    [DllImport("libc", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int write
    (
        int fd,
        byte[] buffer,
        ulong count
    );
    
    [DllImport("libc", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int read
    (
        int fd,
        byte[] buffer,
        ulong count
    );

    public void CreateAndOpenAdapter()
    {
        int fd = open("/dev/net/tun", O_RDWR);
        var ifreq = new Ifreq("JammerTun", IFF_TUN | IFF_NO_PI, new byte[22]);
        
        if (fd<0)
        {
            throw new IOException(
                $"[LinuxTun] не получилось создать адаптер, код ошибки {Marshal.GetLastWin32Error()} ");
        }
        else
        {
            int rc=ioctl(fd, TUNSETIFF, ref ifreq);
            if (rc<0)
            {
                throw new IOException($"[LinuxTun] ошибка ioctl, код ошибки {Marshal.GetLastWin32Error()}");
            }
        }
    }

    public static async Task ConfigureIpAddress()
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo();
        processStartInfo.FileName = "ip";
        processStartInfo.Arguments = "addr add 10.100.0.1/24 dev JammerTun";
        processStartInfo.CreateNoWindow = true;
        processStartInfo.Verb = "runas";
        processStartInfo.UseShellExecute = false;
    
        //добавляем логирование ошибок
        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.RedirectStandardError = true;

        await Task.Delay(1000);

        using (Process process = Process.Start(processStartInfo))
        {
            if (process == null)
            {
                throw new NullReferenceException("[LinuxTun] не удалось присвоить ip адрес интерфейсу");
            }

            process.WaitForExit();
            
            if (process.ExitCode!=0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new IOException($"[LinuxTun] метод ConfigureIpAddress завершился с ошибкой {error}");
            }
            
            Console.WriteLine("[LinuxTun] интерфейсу JammerTun присвоен ip адрес");
        }
    }

    public static async Task StartSession()
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo();
        processStartInfo.FileName = "ip";
        processStartInfo.Arguments = "link set JammerTun up";
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
                throw new NullReferenceException("[LinuxTun] не удалось поднять сетевой интерфейс JammerTun");
            }

            process.WaitForExit();

            if (process.ExitCode!=0)
            {
                var error = process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();
                throw new IOException($"[LinuxTun] метод StartSession завершился с ошибкой {error}");
            }
            
            Console.WriteLine("[LinuxTun] адаптер JammerTun успешно поднят");
        }
    }
}