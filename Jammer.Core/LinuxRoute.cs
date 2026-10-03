using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Jammer.Core;

public class LinuxRoute : IRoute
{

    public void RunProcess(string operand, string command)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo();
        processStartInfo.FileName = $"{operand}";
        processStartInfo.Arguments = command;
        processStartInfo.CreateNoWindow = true;
        processStartInfo.Verb = "runas";
        processStartInfo.UseShellExecute = false;

        using (Process process = Process.Start(processStartInfo))
        {
            if (process==null)
            {
                throw new InvalidOperationException("[LinuxRoute] не удалалось запустить процесс IpRoute");
            }

            process.WaitForExit();

            if (process.ExitCode!=0)
            {
                Console.WriteLine($"[LinuxRoute] команда ip {command} завершилась с ошибкой {process.ExitCode}");
            }
        }
    }
    
    private static (string interfaceName, string gatewayIp) GetActiveNetworkInfo()
    {
        List<string> fakeAdapterList = new List<string>
        {
            "jammer", "wintun", "wireguard", "vpn", "proton", "tap-windows",
            "hyper-v", "virtualbox", "vmware", "virtual",
            "docker", "br-", "veth", "virbr", "tun", "tap", "wg", "amn"
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
        
        throw new InvalidOperationException("[LinuxRoute] не найден активный интерфейс со шлюзом по умолчанию");
        
    }

    public void Route(string serverIp, string? localInterface, string? localGatewayIp)
    {
        if (localInterface==null | localGatewayIp==null)
        {
            var networkInfo = GetActiveNetworkInfo();

            localInterface = networkInfo.interfaceName;
            localGatewayIp = networkInfo.gatewayIp;
        }
        
        RunProcess("ip",$"route add {serverIp}/32 via {localGatewayIp} dev {localInterface}");
        
        RunProcess("ip","route add 0.0.0.0/1 via 192.168.137.1 dev JammerTun");
        
        RunProcess("ip","route add 128.0.0.0/1 via 192.168.137.1 dev JammerTun");
    }

    public void Clean(string serverIp, string localInterface)
    {
        RunProcess("ip",$"route del {serverIp}/32");
        
        RunProcess("ip","route del 0.0.0.0/1");
        
        RunProcess("ip","route del 128.0.0.0/1");
    }

    public void DNS()
    {
        RunProcess("resolvectl",$"dns JammerTun 1.1.1.1");
        
        RunProcess("resolvectl",$"domain JammerTun ~.");
    }
}