using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Market;

/// <summary>
/// Tier 1: Fed funds rate direction — rising rates are bearish, falling/stable are bullish.
/// </summary>
public sealed class InterestRateSignal : IMarketSignal
{
    private readonly MarketSignalWeightsOptions _weights;

    public InterestRateSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.MarketSignalWeights;
    }

    public string Name => "InterestRateEnvironment";
    public double DefaultWeight => _weights.InterestRateEnvironment;

    public Task<SignalResult> EvaluateAsync(MacroIndicators indicators, IReadOnlyList<OhlcvRecord> spOhlcv)
    {
        if (indicators.FedFundsRate is null || indicators.FedFundsRatePreviousMonth is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Fed rate data unavailable", false));

        double current = indicators.FedFundsRate.Value;
        double previous = indicators.FedFundsRatePreviousMonth.Value;
        double change = current - previous;

        if (change < -0.1)
            return Task.FromResult(new SignalResult(Name, 1.0, DefaultWeight,
                $"Fed rate falling: {previous:F2}% → {current:F2}% — bullish for equities", true));

        if (change < 0)
            return Task.FromResult(new SignalResult(Name, 0.5, DefaultWeight,
                $"Fed rate slightly lower: {previous:F2}% → {current:F2}%", true));

        if (Math.Abs(change) < 0.01)
            return Task.FromResult(new SignalResult(Name, 0.25, DefaultWeight,
                $"Fed rate stable at {current:F2}% — neutral/slightly positive", true));

        if (change < 0.25)
            return Task.FromResult(new SignalResult(Name, -0.5, DefaultWeight,
                $"Fed rate rising: {previous:F2}% → {current:F2}% — bearish pressure", true));

        return Task.FromResult(new SignalResult(Name, -1.0, DefaultWeight,
            $"Aggressive Fed rate hike: {previous:F2}% → {current:F2}% — strongly bearish", true));
    }
}
