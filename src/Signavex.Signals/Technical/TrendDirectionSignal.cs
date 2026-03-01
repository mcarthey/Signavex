using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;
using Skender.Stock.Indicators;

namespace Signavex.Signals.Technical;

/// <summary>
/// RAT p.170 — Continuous upward movement from a base forming is a positive signal.
/// Evaluates slope of 20-day closing price trend using linear regression.
/// </summary>
public sealed class TrendDirectionSignal : IStockSignal
{
    private const int LookbackDays = 20;

    private readonly SignalWeightsOptions _weights;

    public TrendDirectionSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "TrendDirection";
    public double DefaultWeight => _weights.TrendDirection;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        if (stock.OhlcvHistory.Count < LookbackDays)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient data", false));

        var quotes = stock.OhlcvHistory.ToQuotes();
        var slopeResults = quotes.GetSlope(LookbackDays).ToList();
        var latest = slopeResults[^1];

        if (latest.Slope is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient data", false));

        double slope = latest.Slope.Value;
        double avgPrice = (double)stock.OhlcvHistory.TakeLast(LookbackDays).Average(r => r.Close);
        double normalizedSlope = slope / avgPrice;

        double score = normalizedSlope switch
        {
            > 0.005 => 1.0,
            > 0.002 => 0.5,
            > 0 => 0.25,
            > -0.002 => -0.25,
            > -0.005 => -0.5,
            _ => -1.0
        };

        string direction = slope > 0 ? "upward" : "downward";
        return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
            $"20-day price trend is {direction} (normalized slope: {normalizedSlope:P2})", true));
    }
}
