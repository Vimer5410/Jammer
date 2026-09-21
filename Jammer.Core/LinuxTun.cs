using System.Runtime.InteropServices;

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

    private const ulong IOC_WRITE = 1;               // направление: мы пишем данные в ядро
    
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
    
}