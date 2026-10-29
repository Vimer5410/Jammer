using System.Net;
using Serilog;

namespace Jammer.Core;

public class JammerLink
{
    private static ILogger _jammerLinkLogger = Log.ForContext("SourceContext", "JammerLink");
    private static int MaxNameLength = 16;
    

    public static Uri CreateLink(string name, string ip, int port)
    {
        string link = $"jammer://{name}@{ip}:{port}";
        
        
        if (IsValidPort(port) && IsValidName(name) && IsValidIpAddress(ip))
        {
            if (Uri.TryCreate(link, UriKind.Absolute, out Uri jammerLink))
            {
                return jammerLink;
            }
        }
        
        throw new ArgumentException("[JammerLink] ошибка создания ссылки");
    }


    public static (string name,string ip, int port) GetValuesFromLink(string link)
    {
        string protocol = null;
        string name = null;
        string ip = null;
        int port = 0;
        
        if (Uri.TryCreate(link, UriKind.Absolute, out Uri jammerLink))
        {
            protocol = jammerLink.Scheme;
            name = jammerLink.UserInfo;
            ip = jammerLink.Host;
            port = jammerLink.Port;
        }
        

        if (protocol=="jammer" && IsValidName(name) && IsValidIpAddress(ip) && IsValidPort(port))
        {
            return (name, ip, port);
        }

        throw new ArgumentException("[JammerLink] ошибка парсинга ссылки");
    }

    private static bool IsValidName(string name)
    {
        bool flag = true;

        if (!string.IsNullOrEmpty(name)  && (name.Length <= MaxNameLength))
        {
            foreach (var el in name)
            {
                if (!(char.IsAsciiLetterOrDigit(el) || el == '-' || el == '_'))
                {
                    flag = false;
                }
            }
        }
        else
        {
            flag = false;
        }

        return flag;
    }


    private static bool IsValidIpAddress(string ip)
    {
        var iptype = Uri.CheckHostName(ip);
        return (iptype == UriHostNameType.IPv4);
    }


    private static bool IsValidPort(int port)
    {
        return (port > 0 && port < 65536);
    }
}