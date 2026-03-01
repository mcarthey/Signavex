using Signavex.Domain.Models;
using Signavex.Engine;

namespace Signavex.Web.Services;

/// <summary>
/// Bridges the scan engine to the Blazor UI. Stores the latest scan results
/// and provides on-demand scan triggering.
/// </summary>
public class ScanResultsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanResultsService> _logger;
    private readonly object _lock = new();

    private IReadOnlyList<StockCandidate> _latestResults = Array.Empty<StockCandidate>();
    private MarketContext? _latestMarketContext;
    private DateTime? _lastScanTime;
    private bool _isScanning;

    public ScanResultsService(IServiceScopeFactory scopeFactory, ILogger<ScanResultsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public IReadOnlyList<StockCandidate> LatestResults
    {
        get { lock (_lock) return _latestResults; }
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

    public event Action? OnScanCompleted;

    public async Task RunScanAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isScanning) return;
            _isScanning = true;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<ScanEngine>();
            var candidates = await engine.RunScanAsync(cancellationToken);

            lock (_lock)
            {
                _latestResults = candidates;
                _latestMarketContext = candidates.FirstOrDefault()?.MarketContext;
                _lastScanTime = DateTime.UtcNow;
            }

            _logger.LogInformation("Scan completed: {Count} candidates", candidates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed");
        }
        finally
        {
            lock (_lock) _isScanning = false;
            OnScanCompleted?.Invoke();
        }
    }
}
