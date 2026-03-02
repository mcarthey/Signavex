namespace Signavex.Web.Services;

/// <summary>
/// On app startup, checks for an incomplete same-day scan checkpoint and auto-resumes it.
/// Ensures interrupted scans continue without user intervention.
/// </summary>
public class ScanResumeBackgroundService : BackgroundService
{
    private readonly ScanResultsService _scanService;
    private readonly ILogger<ScanResumeBackgroundService> _logger;

    public ScanResumeBackgroundService(
        ScanResultsService scanService,
        ILogger<ScanResumeBackgroundService> logger)
    {
        _scanService = scanService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app fully start before checking
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        await _scanService.EnsureInitializedAsync();

        if (await _scanService.HasResumableCheckpointAsync())
        {
            _logger.LogInformation("Found interrupted scan from today — auto-resuming");
            await _scanService.RunScanAsync(stoppingToken);
        }
    }
}
