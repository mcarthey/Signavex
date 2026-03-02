using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Web.Services;

public class ScanDashboardService
{
    private readonly IScanStateStore _stateStore;
    private readonly IScanCommandStore _commandStore;
    private readonly IScanHistoryStore _historyStore;
    private readonly ILogger<ScanDashboardService> _logger;

    public ScanDashboardService(
        IScanStateStore stateStore,
        IScanCommandStore commandStore,
        IScanHistoryStore historyStore,
        ILogger<ScanDashboardService> logger)
    {
        _stateStore = stateStore;
        _commandStore = commandStore;
        _historyStore = historyStore;
        _logger = logger;
    }

    public async Task<ScanStatus> GetScanStatusAsync(CancellationToken ct = default)
    {
        return await _stateStore.GetScanStatusAsync(ct);
    }

    public async Task<CompletedScanResult?> GetLatestResultAsync(CancellationToken ct = default)
    {
        return await _stateStore.LoadLatestResultAsync(ct);
    }

    public async Task<IReadOnlyList<StockCandidate>> GetLiveCandidatesAsync(CancellationToken ct = default)
    {
        var checkpoint = await _stateStore.LoadCheckpointAsync(ct);
        if (checkpoint is null)
            return Array.Empty<StockCandidate>();

        return checkpoint.CandidatesSoFar;
    }

    public async Task<MarketContext?> GetMarketContextAsync(CancellationToken ct = default)
    {
        // Try checkpoint first (active scan), then fall back to latest completed result
        var checkpoint = await _stateStore.LoadCheckpointAsync(ct);
        if (checkpoint is not null)
            return checkpoint.MarketContext;

        var result = await _stateStore.LoadLatestResultAsync(ct);
        return result?.MarketContext;
    }

    public async Task RequestScanAsync(CancellationToken ct = default)
    {
        // Don't enqueue if there's already a pending command
        if (await _commandStore.HasPendingCommandAsync(ct))
        {
            _logger.LogInformation("Scan already pending — skipping duplicate request");
            return;
        }

        await _commandStore.EnqueueCommandAsync("RunScan", ct);
        _logger.LogInformation("Scan requested via dashboard");
    }
}
