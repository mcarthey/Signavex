using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;
using Skender.Stock.Indicators;

namespace Signavex.Signals.Technical;

public sealed class BollingerBandSignal : IStockSignal
{
    private readonly SignalWeightsOptions _weights;

    public BollingerBandSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "BollingerBands";
    public double DefaultWeight => _weights.BollingerBands;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        if (stock.OhlcvHistory.Count < 21)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient data for 20-day Bollinger Bands", false));

        var quotes = stock.OhlcvHistory.ToQuotes();
        var bbResults = quotes.GetBollingerBands(20, 2).ToList();
        var bb = bbResults[^1];

        if (bb.UpperBand is null || bb.LowerBand is null || bb.Sma is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Bollinger Bands calculation unavailable", false));

        var close = (double)stock.OhlcvHistory[^1].Close;
        var upper = bb.UpperBand.Value;
        var lower = bb.LowerBand.Value;
        var mid = bb.Sma.Value;
        var bandwidth = upper - lower;

        if (bandwidth == 0)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Zero bandwidth — flat market", true));

        // Position within the bands: 0 = lower band, 1 = upper band
        var position = (close - lower) / bandwidth;

        var (score, description) = position switch
        {
            < 0 => (1.0, $"Below lower band (${close:F2} < ${lower:F2}) — oversold, potential bounce"),
            < 0.25 => (0.5, $"Near lower band ({position:P0} of range) — price at ${close:F2}, support at ${lower:F2}"),
            < 0.75 => (0.0, $"Mid-range ({position:P0} of range) — price at ${close:F2} between ${lower:F2} and ${upper:F2}"),
            < 1.0 => (-0.5, $"Near upper band ({position:P0} of range) — price at ${close:F2}, resistance at ${upper:F2}"),
            _ => (-1.0, $"Above upper band (${close:F2} > ${upper:F2}) — overbought, potential pullback")
        };

        return Task.FromResult(new SignalResult(Name, score, DefaultWeight, description, true));
    }
}
