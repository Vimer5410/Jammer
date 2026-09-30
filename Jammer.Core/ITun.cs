using System.ComponentModel;

namespace Jammer.Core;

public interface ITun
{
    public void InitializeTunnel();
    public void ConfigureIpAddress(string ipAddress, int maskLength);
    public void StartSession();
    public void SendPacket(byte[] packet);
    public byte[] ReceivePacket();
    
    public static ITun CreateTun()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WinTun();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxTun();
        }

        throw new PlatformNotSupportedException("[ITun] поддержка вашей OS в данный момент не доступна");
    }
}