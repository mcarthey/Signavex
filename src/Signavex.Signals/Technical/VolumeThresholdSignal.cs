using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Technical;

/// <summary>
/// RAT p.169 — Minimum average daily volume of 500,000 shares; a spike above average confirms momentum.
/// </summary>
public sealed class VolumeThresholdSignal : IStockSignal
{
    private const long MinimumAverageDailyVolume = 500_000;
    private const double SpikeMultiplier = 1.5;

    private readonly SignalWeightsOptions _weights;

    public VolumeThresholdSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "VolumeThreshold";
    public double DefaultWeight => _weights.VolumeThreshold;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        if (stock.OhlcvHistory.Count < 20)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient data", false));

        var recent = stock.OhlcvHistory.TakeLast(20).ToList();
        var avgVolume = recent.Average(r => r.Volume);
        var latestVolume = recent[^1].Volume;

        if (avgVolume < MinimumAverageDailyVolume)
            return Task.FromResult(new SignalResult(Name, -0.5, DefaultWeight,
                $"Average volume {avgVolume:N0} is below the 500,000 minimum threshold", true));

        if (latestVolume >= avgVolume * SpikeMultiplier)
            return Task.FromResult(new SignalResult(Name, 1.0, DefaultWeight,
                $"Volume spike: {latestVolume:N0} is {latestVolume / avgVolume:F1}x the 20-day average", true));

        return Task.FromResult(new SignalResult(Name, 0.25, DefaultWeight,
            $"Volume adequate: avg {avgVolume:N0}, latest {latestVolume:N0}", true));
    }
}
