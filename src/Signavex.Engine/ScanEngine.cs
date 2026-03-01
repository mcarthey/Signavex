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

    public async Task<IReadOnlyList<StockCandidate>> RunScanAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Signavex scan at {Time}", DateTime.UtcNow);

        // Step 1: Evaluate market context (Tier 1)
        var macroIndicators = await _economicDataProvider.GetMacroIndicatorsAsync();
        var spOhlcv = (await _marketDataProvider.GetDailyOhlcvAsync("SPY", OhlcvDays)).ToList().AsReadOnly();
        var marketContext = await _marketEvaluator.EvaluateAsync(macroIndicators, spOhlcv);

        _logger.LogInformation("Market context: {Summary} (multiplier: {Multiplier:F2})",
            marketContext.Summary, marketContext.Multiplier);

        // Step 2: Get stock universe
        var universe = await _universeProvider.GetUniverseAsync();
        _logger.LogInformation("Scanning {Count} stocks", universe.Count);

        // Step 3: Evaluate each stock (Tier 2)
        var candidates = new List<StockCandidate>();

        foreach (var (ticker, tier) in universe)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var ohlcv = (await _marketDataProvider.GetDailyOhlcvAsync(ticker, OhlcvDays)).ToList().AsReadOnly();
                var news = (await _newsDataProvider.GetRecentNewsAsync(ticker, NewsDays)).ToList().AsReadOnly();
                var fundamentals = await _fundamentalsProvider.GetFundamentalsAsync(ticker);

                var stockData = new StockData(ticker, ticker, ohlcv, fundamentals, news);
                var candidate = await _stockEvaluator.EvaluateAsync(stockData, marketContext, tier);

                if (candidate is not null)
                {
                    candidates.Add(candidate);
                    _logger.LogInformation("Surfaced: {Ticker} (score: {Score:F3})", ticker, candidate.FinalScore);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate {Ticker} — skipping", ticker);
            }
        }

        _logger.LogInformation("Scan complete. {Count} candidates surfaced.", candidates.Count);
        return candidates.OrderByDescending(c => c.FinalScore).ToList().AsReadOnly();
    }
}
