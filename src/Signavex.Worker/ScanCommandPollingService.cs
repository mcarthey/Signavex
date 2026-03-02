using Signavex.Domain.Interfaces;

namespace Signavex.Worker;

public class ScanCommandPollingService : BackgroundService
{
    private readonly IScanCommandStore _commandStore;
    private readonly WorkerScanOrchestrator _orchestrator;
    private readonly ILogger<ScanCommandPollingService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    public ScanCommandPollingService(
        IScanCommandStore commandStore,
        WorkerScanOrchestrator orchestrator,
        ILogger<ScanCommandPollingService> logger)
    {
        _commandStore = commandStore;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScanCommandPollingService started — polling every {Interval}s", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_orchestrator.IsScanning)
                {
                    var command = await _commandStore.DequeueCommandAsync(stoppingToken);
                    if (command is not null)
                    {
                        _logger.LogInformation("Processing scan command {Id}: {Type}", command.Id, command.CommandType);
                        await _orchestrator.RunScanAsync(command.Id, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling scan commands");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
