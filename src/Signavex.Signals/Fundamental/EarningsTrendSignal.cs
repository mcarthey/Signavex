using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Fundamental;

/// <summary>
/// RAT p.170, p.164–165 — Meeting or exceeding earnings expectations is bullish.
/// Positive earnings surprise amplifies the score.
/// </summary>
public sealed class EarningsTrendSignal : IStockSignal
{
    private readonly SignalWeightsOptions _weights;

    public EarningsTrendSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "EarningsTrend";
    public double DefaultWeight => _weights.EarningsTrend;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        var fundamentals = stock.Fundamentals;
        if (fundamentals?.EpsCurrentQuarter is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Earnings data unavailable", false));

        double eps = fundamentals.EpsCurrentQuarter.Value;

        // Preferred: EPS surprise vs analyst consensus.
        if (fundamentals.EpsEstimateCurrentQuarter is double estimate && estimate != 0)
        {
            double surprise = (eps - estimate) / Math.Abs(estimate);
            double score = ScoreFromSurprise(surprise);
            string direction = surprise >= 0 ? "beat" : "missed";
            return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
                $"EPS {eps:F2} {direction} estimate of {estimate:F2} by {Math.Abs(surprise):P1}", true));
        }

        // Fallback: YoY trend using TTM EPS from OVERVIEW (annualized comparison).
        // Many AlphaVantage tickers don't carry analyst estimates, especially mid-caps.
        // currentQuarter × 4 vs TTM is a reasonable directional proxy.
        if (fundamentals.EpsPreviousYear is double priorTtm && priorTtm != 0)
        {
            double annualized = eps * 4;
            double growth = (annualized - priorTtm) / Math.Abs(priorTtm);
            double score = ScoreFromSurprise(growth);
            string direction = growth >= 0 ? "above" : "below";
            return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
                $"EPS run-rate {annualized:F2} is {Math.Abs(growth):P1} {direction} TTM {priorTtm:F2} (no consensus available)", true));
        }

        return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "No earnings estimate or prior-year EPS available", false));
    }

    private static double ScoreFromSurprise(double surprise) => surprise switch
    {
        > 0.1 => 1.0,
        > 0.03 => 0.75,
        > 0 => 0.25,
        > -0.03 => -0.25,
        > -0.1 => -0.75,
        _ => -1.0
    };
}
