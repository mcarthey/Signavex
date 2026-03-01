using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;
using Skender.Stock.Indicators;

namespace Signavex.Signals.Technical;

/// <summary>
/// RAT p.169 — 14-day MA crossing above the 30-day MA is a bullish signal.
/// </summary>
public sealed class MovingAverageCrossoverSignal : IStockSignal
{
    private readonly SignalWeightsOptions _weights;

    public MovingAverageCrossoverSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "MovingAverageCrossover";
    public double DefaultWeight => _weights.MovingAverageCrossover;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        if (stock.OhlcvHistory.Count < 31)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient data for 30-day MA", false));

        var quotes = stock.OhlcvHistory.ToQuotes();
        var sma14 = quotes.GetSma(14).ToList();
        var sma30 = quotes.GetSma(30).ToList();

        var ma14Today = sma14[^1].Sma;
        var ma30Today = sma30[^1].Sma;
        var ma14Yesterday = sma14[^2].Sma;
        var ma30Yesterday = sma30[^2].Sma;

        if (ma14Today is null || ma30Today is null || ma14Yesterday is null || ma30Yesterday is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient data for 30-day MA", false));

        bool crossedAbove = ma14Today > ma30Today && ma14Yesterday <= ma30Yesterday;
        bool crossedBelow = ma14Today < ma30Today && ma14Yesterday >= ma30Yesterday;
        bool aboveMA = ma14Today > ma30Today;

        if (crossedAbove)
            return Task.FromResult(new SignalResult(Name, 1.0, DefaultWeight,
                $"Bullish crossover: 14-day MA ({ma14Today:F2}) just crossed above 30-day MA ({ma30Today:F2})", true));

        if (crossedBelow)
            return Task.FromResult(new SignalResult(Name, -1.0, DefaultWeight,
                $"Bearish crossover: 14-day MA ({ma14Today:F2}) just crossed below 30-day MA ({ma30Today:F2})", true));

        if (aboveMA)
            return Task.FromResult(new SignalResult(Name, 0.5, DefaultWeight,
                $"14-day MA ({ma14Today:F2}) above 30-day MA ({ma30Today:F2}) — uptrend maintained", true));

        return Task.FromResult(new SignalResult(Name, -0.5, DefaultWeight,
            $"14-day MA ({ma14Today:F2}) below 30-day MA ({ma30Today:F2}) — downtrend", true));
    }
}
