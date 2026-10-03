namespace Jammer.Core;

public interface IRoute
{
    public void Route(string serverIp, string? localInterface, string? localGatewayIp);

    public void Clean(string serverIp, string localInterface);

    public void DNS();

    public static IRoute CreateRoute()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WinRoute();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxRoute();
        }

        throw new PlatformNotSupportedException("[IRoute] поддержка вашей OS в данный момент недоступна");
    }
}