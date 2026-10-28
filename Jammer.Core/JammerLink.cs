using System.Net;
using Serilog;

namespace Jammer.Core;

public class JammerLink
{
    private static ILogger _jammerLinkLogger = Log.ForContext("SourceContext", "JammerLink");
    public static int MaxNameLength = 16;
    

    public Uri CreateLink(string name, string ip, int port)
    {
        string link = $"jammer://{name}@{ip}:{port}";
        bool flag = true;

        if (!string.IsNullOrEmpty(name))
        {
            foreach (var el in name)
            {
                if (!((char.IsAsciiLetterOrDigit(el) || el == '-' || el == '_') && name.Length <= MaxNameLength))
                {
                    flag = false;
                }
            
            }
        }
        else
        {
            flag = false;
        }

        var iptype = Uri.CheckHostName(ip);
        
        if (!(iptype == UriHostNameType.IPv4))
        {
            flag = false;
        }
        
        
        if ((port>0 && port<65536) && flag)
        {
            if (Uri.TryCreate(link, UriKind.Absolute, out Uri jammerLink))
            {
                return jammerLink;
            }
        }
        
        throw new ArgumentException("[JammerLink] ошибка создания ссылки");
    }
}