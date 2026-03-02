using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Signavex.Engine;

/// <summary>
/// Main orchestrator for a Signavex scan run.
/// 1. Fetches macro indicators and evaluates market context (Tier 1)
/// 2. Iterates the configured stock universe
/// 3. For each stock, fetches data and evaluates all signals (Tier 2)
/// 4. Returns all stocks that meet the surfacing threshold
/// </summary>
public class ScanEngine
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly INewsDataProvider _newsDataProvider;
    private readonly IFundamentalsProvider _fundamentalsProvider;
    private readonly IEconomicDataProvider _economicDataProvider;
    private readonly MarketEvaluator _marketEvaluator;
    private readonly StockEvaluator _stockEvaluator;
    private readonly UniverseProvider _universeProvider;
    private readonly ILogger<ScanEngine> _logger;

    private const int OhlcvDays = 250;
    private const int NewsDays = 5;

    public ScanEngine(
        IMarketDataProvider marketDataProvider,
        INewsDataProvider newsDataProvider,
        IFundamentalsProvider fundamentalsProvider,
        IEconomicDataProvider economicDataProvider,
        MarketEvaluator marketEvaluator,
        StockEvaluator stockEvaluator,
        UniverseProvider universeProvider,
        ILogger<ScanEngine> logger)
    {
        _marketDataProvider = marketDataProvider;
        _newsDataProvider = newsDataProvider;
        _fundamentalsProvider = fundamentalsProvider;
        _economicDataProvider = economicDataProvider;
        _marketEvaluator = marketEvaluator;
        _stockEvaluator = stockEvaluator;
        _universeProvider = universeProvider;
        _logger = logger;
    }

    public Task<ScanRunResult> RunScanAsync(CancellationToken cancellationToken = default)
        => RunScanAsync(null, null, null, cancellationToken);

    public Task<ScanRunResult> RunScanAsync(
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken = default)
        => RunScanAsync(progress, null, null, cancellationToken);

    public Task<ScanRunResult> RunScanAsync(
        IProgress<ScanProgress>? progress,
        ScanResumeState? resumeState,
        Func<string, StockCandidate?, Task>? onStockEvaluated,
        CancellationToken cancellationToken = default)
        => RunScanAsync(progress, resumeState, onStockEvaluated, null, cancellationToken);

    public async Task<ScanRunResult> RunScanAsync(
        IProgress<ScanProgress>? progress,
        ScanResumeState? resumeState,
        Func<string, StockCandidate?, Task>? onStockEvaluated,
        Action<MarketContext>? onMarketContextReady,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Evaluate market context (Tier 1) — or reuse from resume state
        MarketContext marketContext;
        if (resumeState is not null)
        {
            marketContext = resumeState.MarketContext;
            _logger.LogInformation("Resuming scan with existing market context (multiplier: {Multiplier:F2})",
                marketContext.Multiplier);
        }
        else
        {
            _logger.LogInformation("Starting Signavex scan at {Time}", DateTime.UtcNow);
            var macroIndicators = await _economicDataProvider.GetMacroIndicatorsAsync();
            var spOhlcv = (await _marketDataProvider.GetDailyOhlcvAsync("SPY", OhlcvDays)).ToList().AsReadOnly();
            marketContext = await _marketEvaluator.EvaluateAsync(macroIndicators, spOhlcv);
        }

        onMarketContextReady?.Invoke(marketContext);

        _logger.LogInformation("Market context: {Summary} (multiplier: {Multiplier:F2})",
            marketContext.Summary, marketContext.Multiplier);

        // Step 2: Get stock universe
        var universe = await _universeProvider.GetUniverseAsync();
        _logger.LogInformation("Scanning {Count} stocks", universe.Count);

        // Step 3: Evaluate each stock (Tier 2)
        var candidates = resumeState?.CandidatesSoFar ?? new List<StockCandidate>();
        var errorCount = resumeState?.PriorErrorCount ?? 0;
        var evaluated = resumeState?.AlreadyEvaluatedTickers.Count ?? 0;

        foreach (var (ticker, tier) in universe)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip already-evaluated tickers on resume
            if (resumeState?.AlreadyEvaluatedTickers.Contains(ticker) == true)
                continue;

            progress?.Report(new ScanProgress(evaluated, universe.Count, ticker, errorCount));

            StockCandidate? candidate = null;
            try
            {
                var ohlcv = (await _marketDataProvider.GetDailyOhlcvAsync(ticker, OhlcvDays)).ToList().AsReadOnly();
                var news = (await _newsDataProvider.GetRecentNewsAsync(ticker, NewsDays)).ToList().AsReadOnly();
                var fundamentals = await _fundamentalsProvider.GetFundamentalsAsync(ticker);

                var stockData = new StockData(ticker, ticker, ohlcv, fundamentals, news);
                candidate = await _stockEvaluator.EvaluateAsync(stockData, marketContext, tier);

                if (candidate is not null)
                {
                    candidates.Add(candidate);
                    _logger.LogInformation("Surfaced: {Ticker} (score: {Score:F3})", ticker, candidate.FinalScore);
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogWarning(ex, "Failed to evaluate {Ticker} — skipping", ticker);
            }

            evaluated++;

            if (onStockEvaluated is not null)
                await onStockEvaluated(ticker, candidate);
        }

        progress?.Report(new ScanProgress(evaluated, universe.Count, "", errorCount));

        _logger.LogInformation("Scan complete. {Count} candidates surfaced, {Errors} errors.", candidates.Count, errorCount);

        var sortedCandidates = candidates.OrderByDescending(c => c.FinalScore).ToList().AsReadOnly();
        return new ScanRunResult(marketContext, sortedCandidates, evaluated, errorCount);
    }
}
