using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThreadDesk.Core;
using ThreadDesk.Infrastructure;
using ThreadDesk.Tally;
using ThreadDesk.Core.Repositories;
using ThreadDesk.Infrastructure.Repositories;
using System.Threading.Tasks;

class Program
{
    public static async Task<int> Main(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        // Use the ThreadDesk.Agent project folder as the configuration base so appsettings.json is found
        var basePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ThreadDesk.Agent"));
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();

        var services = new ServiceCollection();
        services.Configure<AppConfig>(config.GetSection("AppConfig"));
        services.AddLogging(builder => builder.AddConsole());

        services.AddSingleton<ISqliteService, SqliteService>();
        services.AddSingleton<IMigrationService, MigrationService>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<ISyncStateRepository, SyncStateRepository>();

        // Tally provider registration (simple)
        var appcfg = config.GetSection("AppConfig").Get<AppConfig>();
        if (!string.IsNullOrWhiteSpace(appcfg?.TallyOdbcConnectionString))
        {
            services.AddScoped<ITallyConnectionManager, OdbcTallyProvider>();
        }
        else
        {
            services.AddScoped<ITallyConnectionManager, HttpTallyProvider>();
        }

        services.AddScoped<TallySyncService>();

        var sp = services.BuildServiceProvider();

        // Apply migrations
        var migr = sp.GetRequiredService<IMigrationService>();
        await migr.ApplyMigrationsAsync();

        // Run sync
        var sync = sp.GetRequiredService<TallySyncService>();
        try
        {
            await sync.SyncCompaniesAndLedgersAsync();
        }
        catch (Exception ex)
        {
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Tally sync failed");
            return 2;
        }

        // Print counts
        var sqlite = sp.GetRequiredService<ISqliteService>();
        var companies = await sqlite.QueryAsync<int>("SELECT COUNT(*) FROM companies");
        var ledgers = await sqlite.QueryAsync<int>("SELECT COUNT(*) FROM ledgers");
        Console.WriteLine($"companies:{System.Linq.Enumerable.FirstOrDefault(companies)}");
        Console.WriteLine($"ledgers:{System.Linq.Enumerable.FirstOrDefault(ledgers)}");

        return 0;
    }
}
