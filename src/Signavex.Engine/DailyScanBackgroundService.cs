using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Signavex.Engine;

/// <summary>
/// Background service that runs the Signavex scan daily after market close (5 PM ET).
/// Uses IServiceScopeFactory to create a scoped ScanEngine per run.
/// </summary>
public class DailyScanBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyScanBackgroundService> _logger;

    private static readonly TimeSpan RunTime = new(17, 0, 0); // 5:00 PM ET

    public DailyScanBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DailyScanBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyScanBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun();
            _logger.LogInformation("Next scan scheduled in {Delay}", delay);

            await Task.Delay(delay, stoppingToken);

            try
            {
                await RunScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily scan failed");
            }
        }
    }

    internal async Task RunScanAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<ScanEngine>();

        _logger.LogInformation("Starting daily scan at {Time}", DateTime.UtcNow);
        var candidates = await engine.RunScanAsync(cancellationToken);
        _logger.LogInformation("Daily scan complete: {Count} candidates surfaced", candidates.Count);
    }

    private static TimeSpan GetDelayUntilNextRun()
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
        var nextRun = now.Date.Add(RunTime);

        if (now.TimeOfDay >= RunTime)
            nextRun = nextRun.AddDays(1);

        // Skip weekends
        while (nextRun.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }
}
