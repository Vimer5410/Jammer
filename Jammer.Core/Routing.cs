using System.Diagnostics;
using System.Net.NetworkInformation;
using NETCONLib;

namespace Jammer.Core;

public class Routing
{

    /// <summary>
    /// выполнение netsh скриптов
    /// </summary>
    /// <param name="command"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private static void RunNetsh(string command)
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

            process.WaitForExit();
            
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
        List<string> fakeAdapterList = new List<string>
        {
            "jammer", "wintun", "wireguard", "vpn", "proton", "tap-windows", "hyper-v", "virtualbox", "vmware", "virtual"
        };
        
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            string adapter = networkInterface.Name.ToLower();
            
            if (networkInterface.OperationalStatus == OperationalStatus.Up && 
                (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                !fakeAdapterList.Any(el => adapter.Contains(el, StringComparison.OrdinalIgnoreCase)))
                
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

        Console.WriteLine(localInterface);
        
        RunNetsh(
            """interface ipv4 set address name="JammerTun" static 192.168.137.2 255.255.255.0 192.168.137.1""");
        
        RunNetsh(
            $"""interface ipv4 add route prefix={serverIp}/32 interface="{localInterface}" nexthop={localGatewayIp} store=active""");
        
        RunNetsh(
            """interface ipv4 add route prefix=0.0.0.0/1 interface="JammerTun" nexthop=192.168.137.1 metric=1 store=active""");

        RunNetsh(
            """interface ipv4 add route prefix=128.0.0.0/1 interface="JammerTun" nexthop=192.168.137.1 metric=1 store=active""");
        
    }

    
    /// <summary>
    /// Очистка/удаление маршрутов
    /// </summary>
    /// <param name="serverIp"></param>
    /// <param name="localInterface"></param>
    public static void Clean(string serverIp, string localInterface)
    {
        RunNetsh(
            $"""interface ipv4 delete route prefix={serverIp}/32 interface="{localInterface}" """);

        RunNetsh(
            """interface ipv4 delete route prefix=0.0.0.0/1 interface="JammerTun" """);

        RunNetsh(
            """interface ipv4 delete route prefix=128.0.0.0/1 interface="JammerTun" """);
        
    }

    
    /// <summary>
    /// настройка DNS записей, чтобы избежать DNS leak
    /// </summary>
    public static async Task DNS()
    {
        RunNetsh(
            """interface ipv4 set dnsservers name="JammerTun" source=static address=1.1.1.1 register=none""");
        
    }

    /// <summary>
    /// автоматическое выполнение ncpa.cpl--Ethernet--свойства--доступ--разрешить другим пользователям сети...
    /// </summary>
    public static void ICS(string publicAdapter, string privateAdapter)
    {
        NetSharingManagerClass netSharingManager = new NetSharingManagerClass();
        INetSharingEveryConnectionCollection collection = netSharingManager.EnumEveryConnection;


        INetConnection publicConnection = null;
        INetConnection privateConnection = null;
        
        foreach (INetConnection connection in collection)
        {
            var props = netSharingManager.get_NetConnectionProps(connection);

            if (props.Name.Equals(publicAdapter, StringComparison.OrdinalIgnoreCase))
            {
                publicConnection = connection;
            }

            if (props.Name.Equals(privateAdapter, StringComparison.OrdinalIgnoreCase))
            {
                privateConnection = connection;
            }

            if (privateConnection != null && publicConnection != null) break;
            
        }

        if (privateConnection == null)
        {
            throw new NullReferenceException($"[Routing] публичный адаптер {publicAdapter} не найден");
        }

        if (publicConnection == null)
        {
            throw new NullReferenceException($"[Routing] приватный адаптер {privateAdapter} не найден");
        }

        var cfg = netSharingManager.get_INetSharingConfigurationForINetConnection(privateConnection);
        
        try
        {
            var publicCfg = netSharingManager.get_INetSharingConfigurationForINetConnection(publicConnection);
            var privateCfg = netSharingManager.get_INetSharingConfigurationForINetConnection(privateConnection);

            publicCfg.EnableSharing(tagSHARINGCONNECTIONTYPE.ICSSHARINGTYPE_PUBLIC);
            privateCfg.EnableSharing(tagSHARINGCONNECTIONTYPE.ICSSHARINGTYPE_PRIVATE);

            Console.WriteLine($"[Routing] ICS включён: {publicAdapter} → {privateAdapter}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Routing] ошибка включения ICS: {ex.Message}");
            throw;
        }
    }
}