using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ThreadDesk.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ThreadDesk Agent worker running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Heartbeat from worker at: {time}", DateTimeOffset.Now);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (TaskCanceledException) { }
        }

        _logger.LogInformation("ThreadDesk Agent worker stopping.");
    }
}
