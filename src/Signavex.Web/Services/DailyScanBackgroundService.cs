namespace Signavex.Web.Services;

/// <summary>
/// Background service that runs the Signavex scan daily after market close (5 PM ET).
/// Routes through ScanResultsService so daily scans share checkpointing and persistence.
/// </summary>
public class DailyScanBackgroundService : BackgroundService
{
    private readonly ScanResultsService _scanService;
    private readonly ILogger<DailyScanBackgroundService> _logger;

    private static readonly TimeSpan RunTime = new(17, 0, 0); // 5:00 PM ET

    public DailyScanBackgroundService(
        ScanResultsService scanService,
        ILogger<DailyScanBackgroundService> logger)
    {
        _scanService = scanService;
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
                _logger.LogInformation("Starting daily scan at {Time}", DateTime.UtcNow);
                await _scanService.RunScanAsync(stoppingToken);
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

    internal static TimeSpan GetDelayUntilNextRun()
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
