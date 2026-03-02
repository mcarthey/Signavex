namespace Signavex.Worker;

public class ScanResumeBackgroundService : BackgroundService
{
    private readonly WorkerScanOrchestrator _orchestrator;
    private readonly ILogger<ScanResumeBackgroundService> _logger;

    public ScanResumeBackgroundService(
        WorkerScanOrchestrator orchestrator,
        ILogger<ScanResumeBackgroundService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app fully start before checking
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        if (await _orchestrator.HasResumableCheckpointAsync())
        {
            _logger.LogInformation("Found interrupted scan — auto-resuming");
            await _orchestrator.RunScanAsync(cancellationToken: stoppingToken);
        }
    }
}
