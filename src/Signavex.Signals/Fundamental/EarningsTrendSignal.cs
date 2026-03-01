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
        if (fundamentals?.EpsCurrentQuarter is null || fundamentals.EpsEstimateCurrentQuarter is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Earnings data unavailable", false));

        double eps = fundamentals.EpsCurrentQuarter.Value;
        double estimate = fundamentals.EpsEstimateCurrentQuarter.Value;

        if (estimate == 0)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "No earnings estimate available", false));

        double surprise = (eps - estimate) / Math.Abs(estimate);

        double score = surprise switch
        {
            > 0.1 => 1.0,
            > 0.03 => 0.75,
            > 0 => 0.25,
            > -0.03 => -0.25,
            > -0.1 => -0.75,
            _ => -1.0
        };

        string direction = surprise >= 0 ? "beat" : "missed";
        return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
            $"EPS {eps:F2} {direction} estimate of {estimate:F2} by {Math.Abs(surprise):P1}", true));
    }
}
