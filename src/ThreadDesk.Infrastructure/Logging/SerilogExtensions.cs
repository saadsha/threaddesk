using System;
using System.IO;
using Serilog;

namespace ThreadDesk.Infrastructure.Logging;

public static class LogConfigurator
{
    public static void Configure()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var logDir = Path.Combine(programData, "ThreadDesk", "Logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logDir, "agent.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("Logger configured. Logs at {logDir}", logDir);
    }
}
