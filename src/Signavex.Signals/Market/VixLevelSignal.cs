using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Market;

/// <summary>
/// Tier 1: VIX (CBOE Volatility Index) — market fear gauge.
/// Low VIX supports signals; high VIX discounts them.
/// </summary>
public sealed class VixLevelSignal : IMarketSignal
{
    private readonly MarketSignalWeightsOptions _weights;

    public VixLevelSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.MarketSignalWeights;
    }

    public string Name => "VixLevel";
    public double DefaultWeight => _weights.VixLevel;

    public Task<SignalResult> EvaluateAsync(MacroIndicators indicators, IReadOnlyList<OhlcvRecord> spOhlcv)
    {
        if (indicators.VixLevel is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "VIX data unavailable", false));

        double vix = indicators.VixLevel.Value;

        double score = vix switch
        {
            < 15 => 1.0,
            < 20 => 0.5,
            < 25 => 0.0,
            < 30 => -0.5,
            < 40 => -0.75,
            _ => -1.0
        };

        string assessment = vix switch
        {
            < 15 => "very low — market complacent/stable",
            < 20 => "low — calm conditions",
            < 25 => "moderate — normal market volatility",
            < 30 => "elevated — increased fear",
            < 40 => "high — significant market fear",
            _ => "extreme — panic conditions"
        };

        return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
            $"VIX at {vix:F1} — {assessment}", true));
    }
}
