namespace Jammer.Core.Routing;

public interface IRoute
{
    /// <summary>
    /// создание правил маршрутизации для всего трафика через Tun интерфейс, кроме трафика к самому серверу
    /// Вызывать после ConfigureIpAddress(), если интерфейс или шлюз не переданы берет текущие активные
    /// </summary>
    public void Route(string serverIp, string clientIp, string? localInterface, string? localGatewayIp);


    /// <summary>
    /// Очистка/удаление маршрутов созданных Route()
    /// Ошибки только пишутся в лог поэтому можно вызывать повторно и после неудачного подключения
    /// </summary>
    public void Clean(string serverIp, string localInterface);


    /// <summary>
    /// настройка DNS 1.1.1.1 для JammerTun чтобы избежать DNS leak.
    /// Вызывать после ConfigureIpAddress() отдельно не откатывается: настройка пропадает вместе с интерфейсом
    /// </summary>
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