using Signavex.Domain.Models;
using Signavex.Engine;

namespace Signavex.Web.Services;

/// <summary>
/// Bridges BacktestService to the Blazor UI. Manages backtest state and results.
/// </summary>
public class BacktestRunnerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BacktestRunnerService> _logger;
    private readonly object _lock = new();

    private BacktestResult? _latestResult;
    private bool _isRunning;

    public BacktestRunnerService(IServiceScopeFactory scopeFactory, ILogger<BacktestRunnerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public BacktestResult? LatestResult
    {
        get { lock (_lock) return _latestResult; }
    }

    public bool IsRunning
    {
        get { lock (_lock) return _isRunning; }
    }

    public event Action? OnBacktestCompleted;

    public async Task RunBacktestAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BacktestService>();
            var result = await service.RunBacktestAsync(asOfDate, cancellationToken);

            lock (_lock)
            {
                _latestResult = result;
            }

            _logger.LogInformation("Backtest completed for {AsOfDate}: {Count} candidates",
                asOfDate, result.Candidates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backtest failed for {AsOfDate}", asOfDate);
        }
        finally
        {
            lock (_lock) _isRunning = false;
            OnBacktestCompleted?.Invoke();
        }
    }
}
