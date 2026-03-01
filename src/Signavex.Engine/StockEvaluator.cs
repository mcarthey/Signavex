using Signavex.Domain.Configuration;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Engine;

/// <summary>
/// Evaluates all Tier 2 stock-level signals for a single stock and produces a StockCandidate
/// if the FinalScore exceeds the configured surfacing threshold.
/// </summary>
public class StockEvaluator
{
    private readonly IEnumerable<IStockSignal> _signals;
    private readonly ScoreCalculator _scoreCalculator;
    private readonly SignavexOptions _options;

    public StockEvaluator(
        IEnumerable<IStockSignal> signals,
        ScoreCalculator scoreCalculator,
        IOptions<SignavexOptions> options)
    {
        _signals = signals;
        _scoreCalculator = scoreCalculator;
        _options = options.Value;
    }

    public async Task<StockCandidate?> EvaluateAsync(
        StockData stock,
        MarketContext marketContext,
        MarketTier tier)
    {
        var signalTasks = _signals.Select(s => s.EvaluateAsync(stock));
        var results = await Task.WhenAll(signalTasks);

        double rawScore = _scoreCalculator.CalculateWeightedScore(results);
        double finalScore = _scoreCalculator.ApplyMarketMultiplier(rawScore, marketContext.Multiplier);

        double threshold = tier == MarketTier.SP600
            ? 0.75
            : _options.SurfacingThreshold;

        if (finalScore < threshold)
            return null;

        return new StockCandidate(
            stock.Ticker,
            stock.CompanyName,
            tier,
            rawScore,
            finalScore,
            results,
            marketContext,
            DateTime.UtcNow);
    }
}
