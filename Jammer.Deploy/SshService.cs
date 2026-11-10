using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace Jammer.Deploy;

public class SshService
{
    private static ILogger _sshServiceLogger = Log.ForContext("SourceContext", "SshService");
    
    public static async Task DeploySelfHosted(string ip, int port, string name, string password)
    {
        if (IsValidIpAddress(ip) && IsValidPort(port))
        {
            using var ssh = new SshClient(ip, port, name, password);
            await ServerDelivery.DeployFromGithubAsync(ssh);
            UpJammerService(ssh);
        }
    }

    private static void CreateFirewallScript(SshClient ssh)
    {
       
        using var cmd = ssh.RunCommand(
            """
            cat > /opt/jammer/firewall.sh <<'EOF'
            #!/bin/sh
            set -e

            WAN=$(ip route get 1.1.1.1 2>/dev/null | awk '{ for (i = 1; i < NF; i++) if ($i == "dev") { print $(i + 1); exit } }')

            if [ -z "$WAN" ]; then
                echo "jammer-firewall: не найден внешний интерфейс (нет маршрута в интернет)" >&2
                exit 1
            fi

            sysctl -q -w net.ipv4.ip_forward=1

            iptables -w -t nat -C POSTROUTING -s 172.16.0.0/24 -o "$WAN" -j MASQUERADE 2>/dev/null \
                || iptables -w -t nat -A POSTROUTING -s 172.16.0.0/24 -o "$WAN" -j MASQUERADE

            
            iptables -w -C FORWARD -i JammerTun -o "$WAN" -j ACCEPT 2>/dev/null \
                || iptables -w -I FORWARD -i JammerTun -o "$WAN" -j ACCEPT

            iptables -w -C FORWARD -i "$WAN" -o JammerTun -m conntrack --ctstate RELATED,ESTABLISHED -j ACCEPT 2>/dev/null \
                || iptables -w -I FORWARD -i "$WAN" -o JammerTun -m conntrack --ctstate RELATED,ESTABLISHED -j ACCEPT

            echo "jammer-firewall: NAT и пересылка настроены через $WAN"
            EOF
            chmod +x /opt/jammer/firewall.sh
            """.ReplaceLineEndings("\n"));

        if (cmd.ExitStatus!=0)
        {
            throw new DeployException($"не удалось создать скрипт firewall.sh {cmd.ExitStatus}: {cmd.Error}");
        }
    }

    private static void UpJammerService(SshClient sshClient)
    {
        _sshServiceLogger.Information("попытка поднятия служб Jammer Service....");
        
        CreateFirewallScript(sshClient);

        using var systemdCmd = sshClient.RunCommand("systemctl stop jammer; " +
                                             """
                                             cat > /etc/systemd/system/jammer.service <<'EOF'
                                             [Unit]
                                             Description=Jammer VPN server
                                             After=online.target
                                             
                                             [Service]
                                             ExecStart=/opt/jammer/jammer
                                             Environment=DOTNET_BUNDLE_EXTRACT_BASE_DIR=/opt/jammer/.net
                                             ExecStartPre=/opt/jammer/firewall.sh
                                             Restart=on-failure
                                             RestartSec=3
                                             
                                             [Install]
                                             WantedBy=multi-user.target
                                             EOF
                                             """.ReplaceLineEndings("\n"));

        using var upCmd = sshClient.RunCommand("systemctl daemon-reload &&" +
                                               "systemctl enable --now jammer");

        ConfigureServerNetwork(sshClient, null);
        
        using var isServerUpCmd = sshClient.RunCommand("systemctl is-active jammer");
        
        if (systemdCmd.ExitStatus==0 && upCmd.ExitStatus==0 && isServerUpCmd.ExitStatus==0)
        {
            _sshServiceLogger.Information("сервер успешно поднят и работает!");
        }
        else
        {
            _sshServiceLogger.Error("ошибка поднятия служб сервера {status}: {error} {active}", 
                upCmd.ExitStatus, upCmd.Error, isServerUpCmd.Result);
        }
    }
    


    private static void ConfigureServerNetwork(SshClient sshClient, int? port)
    {
        if (!IsValidPort(port))
        {
            port = 7777;
        }

        using var isIptablesDownload = sshClient.RunCommand("command -v iptables >/dev/null || (apt-get update &&" + 
                                                            "DEBIAN_FRONTEND=noninteractive apt-get install -y iptables)");
        if (isIptablesDownload.ExitStatus != 0)
        {
            throw new DeployException($"не удалось установить iptables код {isIptablesDownload.ExitStatus}: {isIptablesDownload.Error}");
        }
        
        
        
        using var nat = sshClient.RunCommand("sysctl -w net.ipv4.ip_forward=1 &&" +
                                             "(iptables -t nat -C POSTROUTING -s 172.16.0.0/24 -o eth0 -j MASQUERADE || iptables -t nat -A POSTROUTING -s 172.16.0.0/24 -o eth0 -j MASQUERADE) &&" +
                                             "echo 'net.ipv4.ip_forward=1' > /etc/sysctl.d/99-jammer.conf");
        if (nat.ExitStatus!=0)
        {
            throw new DeployException($"не удалось настроить NAT {nat.ExitStatus}: {nat.Error}");
        }
        
        
        
        using var ufw = sshClient.RunCommand("! ufw status 2>/dev/null | grep -q \"Status: active\" || (ufw allow 7777/tcp &&" +
                                             "ufw route allow in on JammerTun)");
        if (ufw.ExitStatus!=0)
        {
            throw new DeployException($"не удалось применить правила ufw код {ufw.ExitStatus}: {ufw.Error}");
        }
        
        
        _sshServiceLogger.Information("порт {port} успешно открыт", port);
    }

    private static bool IsValidIpAddress(string ip)
    {
        var iptype=Uri.CheckHostName(ip);
        return (iptype == UriHostNameType.IPv4);
    }

    private static bool IsValidPort(int? port)
    {
        return (port > 0 && port < 65536);
    }
}

public class DeployException : Exception
{
    public DeployException(string message) : base(message) { }
}