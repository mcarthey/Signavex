using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Signavex.Engine;

/// <summary>
/// Runs the existing signal pipeline against date-trimmed OHLCV data
/// to simulate what the scan would have produced on a historical date.
/// Known limitation: news and fundamentals are current-day only (not historically accurate).
/// </summary>
public class BacktestService
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IEconomicDataProvider _economicDataProvider;
    private readonly MarketEvaluator _marketEvaluator;
    private readonly StockEvaluator _stockEvaluator;
    private readonly UniverseProvider _universeProvider;
    private readonly ILogger<BacktestService> _logger;
    private readonly double _surfacingThreshold;

    private const int OhlcvDays = 500; // Fetch extra to allow trimming
    private const string Caveat = "News and fundamental data reflect current values, not historical. " +
                                   "Only OHLCV price data is trimmed to the as-of date.";

    public BacktestService(
        IMarketDataProvider marketDataProvider,
        IEconomicDataProvider economicDataProvider,
        MarketEvaluator marketEvaluator,
        StockEvaluator stockEvaluator,
        UniverseProvider universeProvider,
        IOptions<SignavexOptions> options,
        ILogger<BacktestService> logger)
    {
        _marketDataProvider = marketDataProvider;
        _economicDataProvider = economicDataProvider;
        _marketEvaluator = marketEvaluator;
        _stockEvaluator = stockEvaluator;
        _universeProvider = universeProvider;
        _surfacingThreshold = options.Value.SurfacingThreshold;
        _logger = logger;
    }

    public async Task<BacktestResult> RunBacktestAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting backtest for as-of date {AsOfDate}", asOfDate);

        // Step 1: Evaluate market context with trimmed SPY data
        var macroIndicators = await _economicDataProvider.GetMacroIndicatorsAsync();
        var spyOhlcvFull = (await _marketDataProvider.GetDailyOhlcvAsync("SPY", OhlcvDays)).ToList();
        var spyOhlcv = TrimToDate(spyOhlcvFull, asOfDate);

        var marketContext = await _marketEvaluator.EvaluateAsync(macroIndicators, spyOhlcv);

        _logger.LogInformation("Backtest market context: {Summary} (multiplier: {Multiplier:F2})",
            marketContext.Summary, marketContext.Multiplier);

        // Step 2: Get stock universe
        var universe = await _universeProvider.GetUniverseAsync();
        _logger.LogInformation("Backtesting {Count} stocks as of {AsOfDate}", universe.Count, asOfDate);

        // Step 3: Evaluate each stock with trimmed OHLCV data
        var candidates = new List<StockCandidate>();

        foreach (var (ticker, tier) in universe)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var ohlcvFull = (await _marketDataProvider.GetDailyOhlcvAsync(ticker, OhlcvDays)).ToList();
                var ohlcv = TrimToDate(ohlcvFull, asOfDate);

                if (ohlcv.Count == 0)
                    continue;

                // News and fundamentals are current-day (not historically available)
                var stockData = new StockData(ticker, ticker, ohlcv, null, Array.Empty<NewsItem>());
                var candidate = await _stockEvaluator.EvaluateAsync(stockData, marketContext, tier);
                candidates.Add(candidate);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate {Ticker} in backtest — skipping", ticker);
            }
        }

        var surfaced = candidates.Where(c => c.FinalScore >= _surfacingThreshold).ToList();
        _logger.LogInformation("Backtest complete for {AsOfDate}. {Surfaced}/{Total} candidates surfaced (threshold: {Threshold:F2}).",
            asOfDate, surfaced.Count, candidates.Count, _surfacingThreshold);

        var sorted = surfaced.OrderByDescending(c => c.FinalScore).ToList().AsReadOnly();
        return new BacktestResult(asOfDate, sorted, marketContext, Caveat);
    }

    internal static IReadOnlyList<OhlcvRecord> TrimToDate(List<OhlcvRecord> records, DateOnly asOfDate)
    {
        return records.Where(r => r.Date <= asOfDate).ToList().AsReadOnly();
    }
}
