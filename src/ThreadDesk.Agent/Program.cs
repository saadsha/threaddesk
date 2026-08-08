using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ThreadDesk.Infrastructure.Logging;
using ThreadDesk.Core;
using ThreadDesk.Api;
using ThreadDesk.Tally;

namespace ThreadDesk.Agent;

public class Program
{
    public static async Task Main(string[] args)
    {
        LogConfigurator.Configure();

        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // configuration
                services.Configure<AppConfig>(context.Configuration.GetSection("AppConfig"));

                // infrastructure
                services.AddSingleton<ISqliteService, SqliteService>();

                // api client
                services.AddHttpClient<IThreadDeskApiClient, ThreadDeskApiClient>(client =>
                {
                    // Base address configured by AppConfig or environment
                });

                // Tally
                services.AddSingleton<ITallyConnectionManager, MockTallyProvider>();

                // Hosted worker
                services.AddHostedService<Worker>();
            })
            .Build();

        await host.RunAsync();
    }
}
