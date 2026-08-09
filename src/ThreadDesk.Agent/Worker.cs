using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThreadDesk.Core;
using ThreadDesk.Tally;

namespace ThreadDesk.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AppConfig> _config;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IOptions<AppConfig> config)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ThreadDesk Agent worker running.");
        var interval = TimeSpan.FromMinutes(Math.Max(1, _config.Value.SyncIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var syncService = scope.ServiceProvider.GetRequiredService<TallySyncService>();
                var result = await syncService.SyncCompaniesAndLedgersAsync(stoppingToken);
                _logger.LogInformation("Tally sync finished with status {Succeeded}.", result.Succeeded ? "success" : "failure");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Tally sync worker iteration failed: {FailureType}.", exception.GetType().Name);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("ThreadDesk Agent worker stopping.");
    }
}
