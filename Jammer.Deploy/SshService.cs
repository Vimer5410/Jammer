using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace Jammer.Deploy;

public class SshService
{
    private static ILogger _sshServiceLogger = Log.ForContext("SourceContext", "SshService");
    
    public static void DeploySelfHosted(string ip, int port, string name, string password)
    {
        using var ssh = new SshClient(ip, port, name, password);
        ssh.Connect();

        using var cmd = ssh.RunCommand("mkdir -p /opt/jammer &&"+
                                       "curl -fL -o /opt/jammer/jammer https://github.com/Vimer5410/Jammer/releases/download/v0.1.0/jammer-linux-server-x64 &&"+
                                       "chmod +x /opt/jammer/jammer");

        if (cmd.ExitStatus==0)
        {
            _sshServiceLogger.Information("self-host файл успешно загружен на сервер");
            UpJammerService(ssh);
        }
        else if (cmd.ExitStatus==6)
        {
            _sshServiceLogger.Error("ошибка DNS адресации....");
            using var dnsCmd = ssh.RunCommand("resolvectl dns eth0 1.1.1.1 8.8.8.8");
            
        }
        else
        {
            _sshServiceLogger.Error("ошибка {status}: {error}", cmd.ExitStatus, cmd.Error);
        }
    }

    private static void UpJammerService(SshClient sshClient)
    {
        _sshServiceLogger.Information("Попытка поднятия служб Jammer Service....");

        using var cmd = sshClient.RunCommand("systemctl stop jammer &&" +
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
                                               "systemctl start jammer &&" +
                                               "sysctl -w net.ipv4.ip_forward=1 &&" +
                                               "iptables -t nat -A POSTROUTING -s 172.16.0.0/24 -o eth0 -j MASQUERADE");
        if (cmd.ExitStatus==0 && upCmd.ExitStatus==0)
        {
            _sshServiceLogger.Information("Сервер успешно развернут");
        }
    }


    private static async Task DeployUsingSftp(string ip, int port, string name, string password)
    {
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
            _sshServiceLogger.Information("сервер успешно развернут через sftp");
        }

        
    }
}