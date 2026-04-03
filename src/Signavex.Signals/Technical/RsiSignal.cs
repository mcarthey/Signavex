using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;
using Skender.Stock.Indicators;

namespace Signavex.Signals.Technical;

public sealed class RsiSignal : IStockSignal
{
    private readonly SignalWeightsOptions _weights;

    public RsiSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "Rsi";
    public double DefaultWeight => _weights.Rsi;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        if (stock.OhlcvHistory.Count < 15)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient data for 14-day RSI", false));

        var quotes = stock.OhlcvHistory.ToQuotes();
        var rsiResults = quotes.GetRsi(14).ToList();
        var rsi = rsiResults[^1].Rsi;

        if (rsi is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "RSI calculation unavailable", false));

        var value = rsi.Value;

        var (score, description) = value switch
        {
            < 30 => (1.0, $"Oversold at {value:F1} — potential reversal opportunity"),
            < 40 => (0.5, $"Approaching oversold at {value:F1} — building bullish pressure"),
            < 60 => (0.0, $"Neutral momentum at {value:F1}"),
            < 70 => (-0.5, $"Elevated at {value:F1} — momentum may be peaking"),
            _ => (-1.0, $"Overbought at {value:F1} — potential pullback risk")
        };

        return Task.FromResult(new SignalResult(Name, score, DefaultWeight, description, true));
    }
}
