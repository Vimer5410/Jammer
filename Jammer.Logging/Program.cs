using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

public static class Logging
{
    public static void ConfigureLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                outputTemplate: "{Timestamp:HH:mm:ss} │ {Level:u3} │ {SourceContext,-10} │ {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}