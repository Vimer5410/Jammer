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
            await DeployFromGithubAsync(ip, port, name, password);
        }
    }

    private static void UpJammerService(SshClient sshClient)
    {
        _sshServiceLogger.Information("попытка поднятия служб Jammer Service....");

        using var systemdCmd = sshClient.RunCommand("systemctl stop jammer; " +
                                             """
                                             cat > /etc/systemd/system/jammer.service <<'EOF'
                                             [Unit]
                                             Description=Jammer VPN server
                                             After=network.target
                                             
                                             [Service]
                                             ExecStart=/opt/jammer/jammer
                                             Environment=DOTNET_BUNDLE_EXTRACT_BASE_DIR=/opt/jammer/.net
                                             Restart=on-failure
                                             RestartSec=3
                                             
                                             [Install]
                                             WantedBy=multi-user.target
                                             EOF
                                             """.ReplaceLineEndings("\n"));

        using var upCmd = sshClient.RunCommand("systemctl daemon-reload &&" +
                                               "systemctl enable --now jammer &&" +
                                               "sysctl -w net.ipv4.ip_forward=1 &&" +
                                               "(iptables -t nat -C POSTROUTING -s 172.16.0.0/24 -o eth0 -j MASQUERADE || iptables -t nat -A POSTROUTING -s 172.16.0.0/24 -o eth0 -j MASQUERADE) &&" +
                                               "echo 'net.ipv4.ip_forward=1' > /etc/sysctl.d/99-jammer.conf");

        using var isServerUpCmd = sshClient.RunCommand("systemctl is-active jammer");
        
        if (systemdCmd.ExitStatus==0 && upCmd.ExitStatus==0 && isServerUpCmd.ExitStatus==0)
        {
            _sshServiceLogger.Information("сервер успешно поднят и работает");
        }
        else
        {
            _sshServiceLogger.Error("ошибка поднятия служб сервера {status}: {error} {active}", 
                upCmd.ExitStatus, upCmd.Error, isServerUpCmd.Result);
        }
    }


    private static async Task DeploySftpAsync(string ip, int port, string name, string password)
    {
        _sshServiceLogger.Information("попытка установить сервер через sftp...");
        
        string url = "https://github.com/Vimer5410/Jammer/releases/download/v0.1.0/jammer-linux-server-x64";
        string localPath = Path.Combine(Path.GetTempPath(), "jammer-linux-server-x64");
        
        using var sftp = new SftpClient(ip, port, name, password);
        using var ssh = new SshClient(ip, port, name, password);
        sftp.Connect();
        ssh.Connect();

        using var createDirCmd = ssh.RunCommand("mkdir -p /opt/jammer");
        
        using var http = new HttpClient();
        
        await using var source = await http.GetStreamAsync(url);
        await using var file = File.Create(localPath);
        
        await source.CopyToAsync(file);
        file.Close();
        await using var fs = File.OpenRead(localPath);

        await sftp.UploadFileAsync(fs, "/opt/jammer/jammer.new");
        sftp.ChangePermissions("/opt/jammer/jammer.new", 755);

        using var mvCmd = ssh.RunCommand("mv /opt/jammer/jammer.new /opt/jammer/jammer");

        if (mvCmd.ExitStatus==0)
        {
            _sshServiceLogger.Information("сервер успешно установлен через sftp");
            UpJammerService(ssh);
        }
        else
        {
            _sshServiceLogger.Error("ошибка установки через sftp");
        }

    }

    private static async Task DeployFromGithubAsync(string ip, int port, string name, string password)
    {
        _sshServiceLogger.Information("попытка установить сервер напрямую с github...");
        
        using var ssh = new SshClient(ip, port, name, password);
        ssh.Connect();

        using var cmd = ssh.RunCommand("mkdir -p /opt/jammer &&"+
                                       "curl -fsSL -o /opt/jammer/jammer.new https://github.com/Vimer5410/Jammer/releases/download/v0.1.0/jammer-linux-server-x64 &&" +
                                       "chmod +x /opt/jammer/jammer.new &&" +
                                       "mv /opt/jammer/jammer.new /opt/jammer/jammer");
        

        switch (cmd.ExitStatus)
        {
            case 0:
                _sshServiceLogger.Information("сервер успешно установлен через ssh");
                UpJammerService(ssh);
                break;
            
            case 6:
                _sshServiceLogger.Error("ошибка DNS адресации");
                await DeploySftpAsync(ip, port, name, password);
                break;
            
            case 7:
                _sshServiceLogger.Error("не удалось подключиться");
                await DeploySftpAsync(ip, port, name, password);
                break;
            
            case 28:
                _sshServiceLogger.Error("истекло время ожидания");
                await DeploySftpAsync(ip, port, name, password);
                break;
            
            case 35:
                _sshServiceLogger.Error("ошибка TLS соединения");
                await DeploySftpAsync(ip, port, name, password);
                break;
            
            case 56:
            {
                _sshServiceLogger.Error("соединение оборвалось при получении данных");
                using var newCmd = ssh.RunCommand("mkdir -p /opt/jammer &&"+
                                                  "curl -fL -o /opt/jammer/jammer https://github.com/Vimer5410/Jammer/releases/download/v0.1.0/jammer-linux-server-x64 &&"+
                                                  "chmod +x /opt/jammer/jammer");

                if (newCmd.ExitStatus == 0)
                {
                    _sshServiceLogger.Information("сервер успешно установлен через ssh");
                    UpJammerService(ssh);
                }
                else
                {
                    await DeploySftpAsync(ip, port, name, password);
                }
                
            }
                break;
            
            case 127:
                _sshServiceLogger.Error("команда не найдена");
                await DeploySftpAsync(ip, port, name, password);
                break;

            default:
                _sshServiceLogger.Error("ошибка установки через ssh {status}: {error}", cmd.ExitStatus, cmd.Error);
                break;
        }

    }

    private static bool IsValidIpAddress(string ip)
    {
        var iptype=Uri.CheckHostName(ip);
        return (iptype == UriHostNameType.IPv4);
    }

    private static bool IsValidPort(int port)
    {
        return (port > 0 && port < 65536);
    }
}