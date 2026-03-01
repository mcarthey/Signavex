using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Market;

/// <summary>
/// Tier 1: Is the stock's sector trending up or down overall?
/// Evaluated using sector ETF OHLCV data (provided as spOhlcv parameter for the sector ETF).
/// Note: In the full engine, this is called per-sector using the relevant sector ETF data.
/// </summary>
public sealed class SectorMomentumSignal : IMarketSignal
{
    private const int LookbackDays = 20;

    private readonly MarketSignalWeightsOptions _weights;

    public SectorMomentumSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.MarketSignalWeights;
    }

    public string Name => "SectorMomentum";
    public double DefaultWeight => _weights.SectorMomentum;

    public Task<SignalResult> EvaluateAsync(MacroIndicators indicators, IReadOnlyList<OhlcvRecord> spOhlcv)
    {
        if (spOhlcv.Count < LookbackDays)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient sector ETF data", false));

        var window = spOhlcv.TakeLast(LookbackDays).ToList();
        var priceStart = (double)window.First().Close;
        var priceEnd = (double)window.Last().Close;
        double momentum = (priceEnd - priceStart) / priceStart;

        double score = momentum switch
        {
            > 0.05 => 1.0,
            > 0.02 => 0.5,
            > 0 => 0.25,
            > -0.02 => -0.25,
            > -0.05 => -0.5,
            _ => -1.0
        };

        string direction = momentum >= 0 ? "up" : "down";
        return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
            $"Sector {direction} {Math.Abs(momentum):P1} over {LookbackDays} days", true));
    }
}
