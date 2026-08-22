using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Jammer.Core;

public class Routing
{

    /// <summary>
    /// выполнение netsh скриптов
    /// </summary>
    /// <param name="command"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private static async Task RunNetshAsync(string command)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo();
        processStartInfo.FileName = "netsh";
        processStartInfo.Arguments = command;
        processStartInfo.CreateNoWindow = true;
        processStartInfo.Verb = "runas";
        processStartInfo.UseShellExecute = false;

        using (Process process = Process.Start(processStartInfo))
        {
            if (process==null)
            {
                throw new InvalidOperationException("[Routing] не удалалось запустить процесс netsh");
            }

            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                Console.WriteLine($"[Routing Warning] Команда 'netsh {command}' завершилась с кодом {process.ExitCode}");
            }
        }
    }
    
    /// <summary>
    /// получение текущего имени интерфейса и ip маршрутизатора
    /// </summary>
    /// <returns></returns>
    private static (string interfaceName, string gatewayIp) GetActiveNetworkInfo()
    {
        //fix: убрать нахуй этот спагетти код и переписать все в виде коллекции и добаить проверку не только по имени адаптера но и проверку на WWAN и PPPoE
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            string adapter = networkInterface.Name.ToLower();
            
            // ищем рабочий интерфейс (Ethernet или Wi-Fi) который поднялся и не является виртуальным
            if (networkInterface.OperationalStatus == OperationalStatus.Up && 
                (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                !adapter.Contains("jammer") &&
                !adapter.Contains("wintun") &&
                !adapter.Contains("wireguard") &&
                !adapter.Contains("vpn") &&
                !adapter.Contains("proton") &&
                !adapter.Contains("tap-windows") &&
                !adapter.Contains("hyper-v") &&
                !adapter.Contains("virtualbox") &&
                !adapter.Contains("vmware") &&
                !adapter.Contains("virtual"))
                
            {
                var props = networkInterface.GetIPProperties();
                
                var gateway = props.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                if (gateway != null)
                {
                    return (networkInterface.Name, gateway.Address.ToString());
                }
            }
        }

        //запасной фоллбек если ничего не нашлось
        return ("Ethernet", "192.168.1.1");
    }
    
    
    /// <summary>
    /// роутинг всего трафика через TUN с исключением сервера
    /// </summary>
    /// <param name="serverIp"></param>
    /// <param name="localInterface"></param>
    /// <param name="localGatewayIp"></param>
    public static async Task Route(string serverIp, string? localInterface, string? localGatewayIp)
    {
        if (localInterface==null || localGatewayIp==null)
        {
            var networkInfo = GetActiveNetworkInfo();
            localInterface = networkInfo.interfaceName;
            localGatewayIp = networkInfo.gatewayIp;
        }
        await RunNetshAsync(
            $"""interface ipv4 add route prefix={serverIp}/32 interface="{localInterface}" nexthop={localGatewayIp} store=active""");
        
        await RunNetshAsync(
            """interface ipv4 add route prefix=0.0.0.0/1 interface="JammerTun" nexthop=172.16.0.1 metric=1 store=active""");

        await RunNetshAsync(
            """interface ipv4 add route prefix=128.0.0.0/1 interface="JammerTun" nexthop=172.16.0.1 metric=1 store=active""");
        
    }

    
    /// <summary>
    /// Очистка/удаление маршрутов
    /// </summary>
    /// <param name="serverIp"></param>
    /// <param name="localInterface"></param>
    public static async Task Clean(string serverIp, string localInterface)
    {
        await RunNetshAsync(
            $"""interface ipv4 delete route prefix={serverIp}/32 interface="{localInterface}" """);

        await RunNetshAsync(
            """interface ipv4 delete route prefix=0.0.0.0/1 interface="JammerTun" """);

        await RunNetshAsync(
            """interface ipv4 delete route prefix=128.0.0.0/1 interface="JammerTun" """);
        
    }

    
    /// <summary>
    /// настройка DNS записей, чтобы избежать DNS leak
    /// </summary>
    public static async Task DNS()
    {
        await RunNetshAsync(
            """interface ipv4 set dnsservers name="JammerTun" source=static address=1.1.1.1 register=none""");
        
    }
}