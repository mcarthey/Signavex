using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;
using Skender.Stock.Indicators;

namespace Signavex.Signals.Technical;

public sealed class MacdSignal : IStockSignal
{
    private readonly SignalWeightsOptions _weights;

    public MacdSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "Macd";
    public double DefaultWeight => _weights.Macd;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        if (stock.OhlcvHistory.Count < 35)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient data for MACD (26+9 periods)", false));

        var quotes = stock.OhlcvHistory.ToQuotes();
        var macdResults = quotes.GetMacd(12, 26, 9).ToList();

        var today = macdResults[^1];
        var yesterday = macdResults[^2];

        if (today.Macd is null || today.Signal is null || yesterday.Macd is null || yesterday.Signal is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "MACD calculation unavailable", false));

        var macd = today.Macd.Value;
        var signal = today.Signal.Value;
        var prevMacd = yesterday.Macd.Value;
        var prevSignal = yesterday.Signal.Value;
        var histogram = today.Histogram ?? 0;

        bool crossedAbove = macd > signal && prevMacd <= prevSignal;
        bool crossedBelow = macd < signal && prevMacd >= prevSignal;

        if (crossedAbove)
            return Task.FromResult(new SignalResult(Name, 1.0, DefaultWeight,
                $"Bullish MACD crossover — MACD ({macd:F3}) just crossed above signal ({signal:F3})", true));

        if (crossedBelow)
            return Task.FromResult(new SignalResult(Name, -1.0, DefaultWeight,
                $"Bearish MACD crossover — MACD ({macd:F3}) just crossed below signal ({signal:F3})", true));

        if (macd > signal && histogram > 0)
            return Task.FromResult(new SignalResult(Name, 0.5, DefaultWeight,
                $"MACD ({macd:F3}) above signal ({signal:F3}) — bullish momentum", true));

        if (macd < signal && histogram < 0)
            return Task.FromResult(new SignalResult(Name, -0.5, DefaultWeight,
                $"MACD ({macd:F3}) below signal ({signal:F3}) — bearish momentum", true));

        return Task.FromResult(new SignalResult(Name, 0.0, DefaultWeight,
            $"MACD ({macd:F3}) near signal ({signal:F3}) — neutral", true));
    }
}
