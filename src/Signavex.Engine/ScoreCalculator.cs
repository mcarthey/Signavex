using Signavex.Domain.Models;

namespace Signavex.Engine;

/// <summary>
/// Computes weighted stock scores and applies the market context multiplier.
/// Formula: StockScore = Sum(score * weight) / Sum(weight)
/// FinalScore = StockScore * MarketMultiplier
/// </summary>
public class ScoreCalculator
{
    public double CalculateWeightedScore(IEnumerable<SignalResult> signalResults)
    {
        var available = signalResults.Where(r => r.IsAvailable).ToList();
        if (available.Count == 0) return 0;

        double weightedSum = available.Sum(r => r.Score * r.Weight);
        double totalWeight = available.Sum(r => r.Weight);

        return totalWeight == 0 ? 0 : weightedSum / totalWeight;
    }

    public double ApplyMarketMultiplier(double stockScore, double marketMultiplier)
        => stockScore * marketMultiplier;
}
