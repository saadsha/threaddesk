using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ThreadDesk.Infrastructure.Logging;
using ThreadDesk.Core;
using ThreadDesk.Api;
using ThreadDesk.Tally;
using ThreadDesk.Api.Authentication;
using ThreadDesk.Api.Http;
using ThreadDesk.Infrastructure;
using ThreadDesk.Infrastructure.Security;
using ThreadDesk.Core.Repositories;
using ThreadDesk.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

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
                services.AddSingleton<IMigrationService, MigrationService>();

                // credential store
                services.AddSingleton<ICredentialStore, DpapiCredentialStore>();

                // register auth service (typed client)
                services.AddHttpClient("ApiBase", (sp, client) =>
                {
                    var cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;
                    client.BaseAddress = new Uri(cfg.ApiBaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(30);
                });

                // AuthenticationService uses the named client
                services.AddScoped<IAuthenticationService>(sp =>
                {
                    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiBase");
                    var cfg = sp.GetRequiredService<IOptions<AppConfig>>();
                    var creds = sp.GetRequiredService<ICredentialStore>();
                    var logger = sp.GetRequiredService<ILogger<AuthenticationService>>();
                    return new AuthenticationService(http, cfg, creds, logger);
                });

                // Token handler for API calls
                services.AddTransient<TokenDelegatingHandler>(sp =>
                {
                    var cred = sp.GetRequiredService<ICredentialStore>();
                    var auth = sp.GetRequiredService<IAuthenticationService>();
                    var logger = sp.GetRequiredService<ILogger<TokenDelegatingHandler>>();
                    var agentVersion = sp.GetRequiredService<IOptions<AppConfig>>().Value.AgentVersion;
                    return new TokenDelegatingHandler(cred, auth, logger, agentVersion);
                });

                // ThreadDesk API client using delegating handler
                services.AddHttpClient<IThreadDeskApiClient, ThreadDeskApiClient>((sp, client) =>
                {
                    var cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;
                    client.BaseAddress = new Uri(cfg.ApiBaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(60);
                })
                .AddHttpMessageHandler<TokenDelegatingHandler>();

                // Tally - prefer ODBC provider if connection string configured, otherwise fall back to HTTP provider
                services.AddScoped<ITallyConnectionManager>(sp =>
                {
                    var cfg = sp.GetRequiredService<IOptions<AppConfig>>().Value;
                    var loggerFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
                    if (!string.IsNullOrWhiteSpace(cfg.TallyOdbcConnectionString))
                    {
                        var logger = loggerFactory.CreateLogger<ThreadDesk.Tally.OdbcTallyProvider>();
                        return (ITallyConnectionManager)new ThreadDesk.Tally.OdbcTallyProvider(sp.GetRequiredService<IOptions<AppConfig>>(), logger);
                    }
                    else
                    {
                        // create HttpTallyProvider with a client
                        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                        var client = httpClientFactory.CreateClient();
                        client.BaseAddress = new Uri(cfg.TallyBaseUrl);
                        client.Timeout = TimeSpan.FromSeconds(30);
                        return new ThreadDesk.Tally.HttpTallyProvider(client);
                    }
                });

                // Repositories
                services.AddScoped<ICompanyRepository, CompanyRepository>();
                services.AddScoped<ISyncStateRepository, SyncStateRepository>();
                services.AddScoped<ILedgerRepository, LedgerRepository>();

                // Tally sync service
                services.AddScoped<TallySyncService>();

                // Hosted worker
                services.AddHostedService<Worker>();
            })
            .Build();

        // Apply DB migrations before starting the host so repositories have expected schema
        var migrationService = host.Services.GetRequiredService<IMigrationService>();
        await migrationService.ApplyMigrationsAsync();

        await host.RunAsync();
    }
}