using Renci.SshNet;
using Serilog;

namespace Jammer.Deploy;

public class ServerDelivery
{
    private static ILogger _serverDeliveryLogger = Log.ForContext("SourceContext", "ServerDelivery");
    
    
    public static async Task DeployFromGithubAsync(SshClient ssh)
    {
        _serverDeliveryLogger.Information("попытка установить сервер напрямую с github...");
        
        
        ssh.Connect();

        using var cmd = ssh.RunCommand("mkdir -p /opt/jammer &&"+
                                       "curl -fsSL -o /opt/jammer/jammer.new https://github.com/Vimer5410/Jammer/releases/download/v0.1.0/jammer-linux-server-x64 &&" +
                                       "chmod +x /opt/jammer/jammer.new &&" +
                                       "mv /opt/jammer/jammer.new /opt/jammer/jammer");
        

        switch (cmd.ExitStatus)
        {
            case 0:
                _serverDeliveryLogger.Information("сервер успешно установлен через ssh");
                break;
            
            case 6:
                _serverDeliveryLogger.Error("ошибка DNS адресации");
                await DeploySftpAsync(ssh);
                break;
            
            case 7:
                _serverDeliveryLogger.Error("не удалось подключиться");
                await DeploySftpAsync(ssh);
                break;
            
            case 28:
                _serverDeliveryLogger.Error("истекло время ожидания");
                await DeploySftpAsync(ssh);
                break;
            
            case 35:
                _serverDeliveryLogger.Error("ошибка TLS соединения");
                await DeploySftpAsync(ssh);
                break;
            
            case 56:
            {
                _serverDeliveryLogger.Error("соединение оборвалось при получении данных");
                using var newCmd = ssh.RunCommand("mkdir -p /opt/jammer &&"+
                                                  "curl -fL -o /opt/jammer/jammer https://github.com/Vimer5410/Jammer/releases/download/v0.1.0/jammer-linux-server-x64 &&"+
                                                  "chmod +x /opt/jammer/jammer");

                if (newCmd.ExitStatus == 0)
                {
                    _serverDeliveryLogger.Information("сервер успешно установлен через ssh");
                }
                else
                {
                    await DeploySftpAsync(ssh);
                }
                
            }
                break;
            
            case 127:
                _serverDeliveryLogger.Error("команда не найдена");
                await DeploySftpAsync(ssh);
                break;

            default:
                throw new DeployException($"ошибка установки через ssh {cmd.ExitStatus}: {cmd.Error}");
        }

    }
    
    
    
    private static async Task DeploySftpAsync(SshClient ssh)
    {
        _serverDeliveryLogger.Information("попытка установить сервер через sftp...");
        
        string url = "https://github.com/Vimer5410/Jammer/releases/download/v0.1.0/jammer-linux-server-x64";
        string localPath = Path.Combine(Path.GetTempPath(), "jammer-linux-server-x64");
        
        using var sftp = new SftpClient(ssh.ConnectionInfo);
        
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
            _serverDeliveryLogger.Information("сервер успешно установлен через sftp");
        }
        else
        {
            throw new DeployException("ошибка установки через sftp");
        }

    }
    
    
    public class DeployException : Exception
    {
        public DeployException(string message) : base(message) { }
    }
}
