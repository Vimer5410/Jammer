using System.Runtime.InteropServices;

namespace Jammer.Core;

public class LinuxTun
{
    //fix: посчитать значения для каждой константы 
    
    private const int O_RDWR;
    
    //fix: написать готовую формулу для вычисления _IOW без хардкода 0x400454ca
    private const ulong TUNSETIFF;

    private const short IFF_TUN;

    private const short IFF_NO_PI;
    
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