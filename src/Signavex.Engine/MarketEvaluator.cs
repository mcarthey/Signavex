using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Engine;

/// <summary>
/// Evaluates all Tier 1 market-level signals and produces a MarketContext
/// with a multiplier (0.5–1.5) that gates/scales all stock-level scores.
/// </summary>
public class MarketEvaluator
{
    private readonly IEnumerable<IMarketSignal> _signals;
    private readonly ScoreCalculator _scoreCalculator;

    public MarketEvaluator(IEnumerable<IMarketSignal> signals, ScoreCalculator scoreCalculator)
    {
        _signals = signals;
        _scoreCalculator = scoreCalculator;
    }

    public async Task<MarketContext> EvaluateAsync(
        MacroIndicators indicators,
        IReadOnlyList<OhlcvRecord> spOhlcv)
    {
        var signalTasks = _signals.Select(s => s.EvaluateAsync(indicators, spOhlcv));
        var results = await Task.WhenAll(signalTasks);

        double marketScore = _scoreCalculator.CalculateWeightedScore(results);

        // Map score (-1 to 1) to multiplier (0.5 to 1.5)
        double multiplier = 1.0 + (marketScore * 0.5);
        multiplier = Math.Clamp(multiplier, 0.5, 1.5);

        string summary = multiplier switch
        {
            >= 1.3 => "Bullish macro: strong market conditions amplifying signals",
            >= 1.1 => "Mildly bullish macro conditions",
            >= 0.9 => "Neutral macro conditions",
            >= 0.7 => "Mildly bearish macro: discounting stock signals",
            _ => "Bearish macro: poor conditions significantly discounting signals"
        };

        return new MarketContext(multiplier, summary, results);
    }
}
