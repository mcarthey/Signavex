using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Engine;
using Signavex.Infrastructure.Persistence;

namespace Signavex.Worker;

public class WorkerScanOrchestrator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScanStateStore _stateStore;
    private readonly IScanCommandStore _commandStore;
    private readonly ILogger<WorkerScanOrchestrator> _logger;
    private readonly object _lock = new();
    private bool _isScanning;

    public bool IsScanning
    {
        get { lock (_lock) return _isScanning; }
    }

    public WorkerScanOrchestrator(
        IServiceScopeFactory scopeFactory,
        IScanStateStore stateStore,
        IScanCommandStore commandStore,
        ILogger<WorkerScanOrchestrator> logger)
    {
        _scopeFactory = scopeFactory;
        _stateStore = stateStore;
        _commandStore = commandStore;
        _logger = logger;
    }

    public async Task<bool> HasResumableCheckpointAsync()
    {
        try
        {
            var checkpoint = await _stateStore.LoadCheckpointAsync();
            return checkpoint is not null && IsCheckpointRecent(checkpoint);
        }
        catch
        {
            return false;
        }
    }

    public async Task RunScanAsync(int? commandId = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isScanning) return;
            _isScanning = true;
        }

        var scanStartTime = DateTime.UtcNow;
        ScanResumeState? resumeState = null;
        var initialEvaluated = 0;
        string scanId;

        // Check for existing checkpoint (within 48-hour window)
        try
        {
            var checkpoint = await _stateStore.LoadCheckpointAsync(cancellationToken);
            if (checkpoint is not null)
            {
                if (IsCheckpointRecent(checkpoint))
                {
                    resumeState = new ScanResumeState(
                        checkpoint.MarketContext,
                        new HashSet<string>(checkpoint.EvaluatedTickers),
                        checkpoint.CandidatesSoFar.ToList(),
                        checkpoint.ErrorCount);

                    initialEvaluated = checkpoint.EvaluatedTickers.Count;
                    scanId = checkpoint.ScanId;

                    _logger.LogInformation("Resuming scan {ScanId} from checkpoint: {Evaluated} already evaluated",
                        scanId, initialEvaluated);
                }
                else
                {
                    _logger.LogInformation("Discarding stale checkpoint from {Date}", checkpoint.StartedAtUtc.Date);
                    await _stateStore.DeleteCheckpointAsync(cancellationToken);
                    scanId = Guid.NewGuid().ToString("N")[..8];
                }
            }
            else
            {
                scanId = Guid.NewGuid().ToString("N")[..8];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load checkpoint — starting fresh scan");
            scanId = Guid.NewGuid().ToString("N")[..8];
        }

        // Track evaluated tickers for checkpointing
        var evaluatedTickers = new List<string>(resumeState?.AlreadyEvaluatedTickers ?? new HashSet<string>());
        var allCandidates = new List<StockCandidate>(resumeState?.CandidatesSoFar ?? new List<StockCandidate>());
        MarketContext? marketContext = resumeState?.MarketContext;
        var lastKnownErrorCount = resumeState?.PriorErrorCount ?? 0;
        IReadOnlyList<string>? universeTickers = null;

        // Progress callback — captures total from engine
        var lastKnownTotal = 0;
        var progress = new Progress<ScanProgress>(p =>
        {
            lastKnownErrorCount = p.ErrorCount;
            lastKnownTotal = p.Total;

            if (p.Evaluated % 50 == 0 || p.Evaluated == p.Total)
            {
                _logger.LogInformation("Scan progress: {Evaluated}/{Total} — {Ticker} — {Candidates} candidates, {Errors} errors",
                    p.Evaluated, p.Total, p.CurrentTicker, allCandidates.Count, p.ErrorCount);
            }
        });
        Func<string, StockCandidate, Task> onStockEvaluated = async (ticker, candidate) =>
        {
            evaluatedTickers.Add(ticker);
            allCandidates.Add(candidate);

            // Save checkpoint with scalar progress columns
            try
            {
                if (marketContext is not null)
                {
                    var checkpointData = new ScanCheckpoint(
                        scanId, scanStartTime, marketContext,
                        universeTickers ?? evaluatedTickers.AsReadOnly(),
                        evaluatedTickers.AsReadOnly(),
                        allCandidates.AsReadOnly(),
                        lastKnownErrorCount);

                    await _stateStore.SaveCheckpointAsync(checkpointData, lastKnownTotal, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save checkpoint after {Ticker}", ticker);
            }
        };

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<ScanEngine>();
            var universeProvider = scope.ServiceProvider.GetRequiredService<UniverseProvider>();
            universeTickers = (await universeProvider.GetUniverseAsync())
                .Select(u => u.Ticker).ToList().AsReadOnly();
            lastKnownTotal = universeTickers.Count;

            var result = await engine.RunScanAsync(progress, resumeState, onStockEvaluated, ctx =>
            {
                marketContext = ctx;
            }, cancellationToken);

            // Save completed result
            var completed = new CompletedScanResult(
                scanId, DateTime.UtcNow, result.MarketContext,
                result.Candidates, result.TotalEvaluated, result.ErrorCount);

            await _stateStore.SaveCompletedResultAsync(completed);
            await _stateStore.DeleteCheckpointAsync();

            // Mark the triggering command as completed
            if (commandId.HasValue)
            {
                try
                {
                    await _commandStore.CompleteCommandAsync(commandId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to complete command {CommandId}", commandId.Value);
                }
            }

            _logger.LogInformation("Scan {ScanId} completed: {Count} candidates, {Errors} errors",
                scanId, result.Candidates.Count, result.ErrorCount);

            // Trigger daily brief generation after successful scan
            try
            {
                await _commandStore.EnqueueCommandAsync(DailyBriefBackgroundService.CommandType, CancellationToken.None);
                _logger.LogInformation("Enqueued daily brief generation after scan {ScanId}", scanId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enqueue daily brief generation after scan {ScanId}", scanId);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scan {ScanId} was cancelled — checkpoint preserved for resume", scanId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan {ScanId} failed — checkpoint preserved for resume", scanId);
        }
        finally
        {
            // Mark checkpoint as inactive so Web UI knows scan is no longer running
            try
            {
                if (_stateStore is SqliteScanStateStore sqliteStore)
                    await sqliteStore.SetCheckpointInactiveAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark checkpoint inactive");
            }

            lock (_lock)
            {
                _isScanning = false;
            }
        }
    }

    private static bool IsCheckpointRecent(ScanCheckpoint checkpoint)
        => (DateTime.UtcNow - checkpoint.StartedAtUtc).TotalHours < 48;
}
