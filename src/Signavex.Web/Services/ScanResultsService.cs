using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Engine;

namespace Signavex.Web.Services;

/// <summary>
/// Bridges the scan engine to the Blazor UI. Orchestrates scan execution with
/// checkpoint persistence, resume capability, and live candidate reporting.
/// </summary>
public class ScanResultsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScanStateStore _stateStore;
    private readonly ILogger<ScanResultsService> _logger;
    private readonly object _lock = new();

    private IReadOnlyList<StockCandidate> _latestResults = Array.Empty<StockCandidate>();
    private List<StockCandidate> _liveCandidates = new();
    private MarketContext? _latestMarketContext;
    private DateTime? _lastScanTime;
    private bool _isScanning;
    private ScanProgress? _currentProgress;
    private int _lastScanErrors;
    private bool _initialized;

    public ScanResultsService(
        IServiceScopeFactory scopeFactory,
        IScanStateStore stateStore,
        ILogger<ScanResultsService> logger)
    {
        _scopeFactory = scopeFactory;
        _stateStore = stateStore;
        _logger = logger;
    }

    public IReadOnlyList<StockCandidate> LatestResults
    {
        get { lock (_lock) return _latestResults; }
    }

    public IReadOnlyList<StockCandidate> LiveCandidates
    {
        get { lock (_lock) return _liveCandidates.ToList().AsReadOnly(); }
    }

    public MarketContext? LatestMarketContext
    {
        get { lock (_lock) return _latestMarketContext; }
    }

    public DateTime? LastScanTime
    {
        get { lock (_lock) return _lastScanTime; }
    }

    public bool IsScanning
    {
        get { lock (_lock) return _isScanning; }
    }

    public ScanProgress? CurrentProgress
    {
        get { lock (_lock) return _currentProgress; }
    }

    public int LastScanErrors
    {
        get { lock (_lock) return _lastScanErrors; }
    }

    public event Action? OnScanCompleted;
    public event Action? OnProgressChanged;
    public event Action? OnCandidateFound;

    /// <summary>
    /// Loads the last completed scan result from disk. Falls back to checkpoint
    /// candidates if no completed scan exists. Called once on first access.
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        try
        {
            var completed = await _stateStore.LoadLatestResultAsync();
            if (completed is not null)
            {
                lock (_lock)
                {
                    _latestResults = completed.Candidates;
                    _latestMarketContext = completed.MarketContext;
                    _lastScanTime = completed.CompletedAtUtc;
                    _lastScanErrors = completed.ErrorCount;
                }
                _logger.LogInformation("Loaded persisted scan results: {Count} candidates from {Time}",
                    completed.Candidates.Count, completed.CompletedAtUtc);
            }
            else
            {
                // No completed scan — check for checkpoint candidates so the dashboard isn't empty
                var checkpoint = await _stateStore.LoadCheckpointAsync();
                if (checkpoint is not null && checkpoint.CandidatesSoFar.Count > 0)
                {
                    lock (_lock)
                    {
                        _latestResults = checkpoint.CandidatesSoFar;
                        _latestMarketContext = checkpoint.MarketContext;
                        _lastScanTime = checkpoint.StartedAtUtc;
                    }
                    _logger.LogInformation(
                        "No completed scan — loaded {Count} candidates from checkpoint {ScanId}",
                        checkpoint.CandidatesSoFar.Count, checkpoint.ScanId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load persisted scan results");
        }

        _initialized = true;
    }

    /// <summary>
    /// Returns true if a recent checkpoint exists for resume (within 48 hours).
    /// </summary>
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

    private static bool IsCheckpointRecent(ScanCheckpoint checkpoint)
        => (DateTime.UtcNow - checkpoint.StartedAtUtc).TotalHours < 48;

    public async Task RunScanAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        lock (_lock)
        {
            if (_isScanning) return;
            _isScanning = true;
            _currentProgress = null;
            _liveCandidates = new List<StockCandidate>();
        }

        var scanStartTime = DateTime.UtcNow;
        ScanResumeState? resumeState = null;
        var isResuming = false;
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

                    isResuming = true;
                    initialEvaluated = checkpoint.EvaluatedTickers.Count;
                    scanId = checkpoint.ScanId;

                    lock (_lock)
                    {
                        _liveCandidates = checkpoint.CandidatesSoFar.ToList();
                        _latestMarketContext = checkpoint.MarketContext;
                    }

                    OnCandidateFound?.Invoke();

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

        // Progress callback with ETA calculation
        var progress = new Progress<ScanProgress>(p =>
        {
            var evaluatedThisSession = p.Evaluated - initialEvaluated;
            TimeSpan? eta = null;

            if (evaluatedThisSession > 0)
            {
                var elapsed = DateTime.UtcNow - scanStartTime;
                var avgPerStock = elapsed / evaluatedThisSession;
                var remaining = p.Total - p.Evaluated;
                eta = avgPerStock * remaining;
            }

            var enhanced = new EnhancedScanProgress(
                p.Evaluated, p.Total, p.CurrentTicker, p.ErrorCount,
                allCandidates.Count, isResuming, eta);

            lock (_lock) _currentProgress = enhanced;
            OnProgressChanged?.Invoke();
        });

        // Stock evaluation callback — updates live candidates and saves checkpoint
        var universeTickers = new List<string>();
        Func<string, StockCandidate?, Task> onStockEvaluated = async (ticker, candidate) =>
        {
            evaluatedTickers.Add(ticker);

            if (candidate is not null)
            {
                allCandidates.Add(candidate);
                lock (_lock)
                {
                    _liveCandidates = allCandidates.ToList();
                }
                OnCandidateFound?.Invoke();
            }

            // Save checkpoint
            try
            {
                if (marketContext is not null)
                {
                    var checkpoint = new ScanCheckpoint(
                        scanId, scanStartTime, marketContext,
                        universeTickers.AsReadOnly(),
                        evaluatedTickers.AsReadOnly(),
                        allCandidates.AsReadOnly(),
                        allCandidates.Count);

                    await _stateStore.SaveCheckpointAsync(checkpoint, CancellationToken.None);
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

            // Capture universe tickers for checkpoint (engine fetches this internally,
            // but we need it for the checkpoint). We'll get it from the first progress report.
            var result = await engine.RunScanAsync(progress, resumeState, onStockEvaluated, cancellationToken);

            marketContext = result.MarketContext;

            // Save completed result
            var completed = new CompletedScanResult(
                scanId, DateTime.UtcNow, result.MarketContext,
                result.Candidates, result.TotalEvaluated, result.ErrorCount);

            await _stateStore.SaveCompletedResultAsync(completed);
            await _stateStore.DeleteCheckpointAsync();

            lock (_lock)
            {
                _latestResults = result.Candidates;
                _latestMarketContext = result.MarketContext;
                _lastScanTime = DateTime.UtcNow;
                _lastScanErrors = result.ErrorCount;
                _liveCandidates = new List<StockCandidate>();
            }

            _logger.LogInformation("Scan {ScanId} completed: {Count} candidates", scanId, result.Candidates.Count);
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
            lock (_lock)
            {
                _isScanning = false;
                _currentProgress = null;
            }
            OnScanCompleted?.Invoke();
        }
    }
}
