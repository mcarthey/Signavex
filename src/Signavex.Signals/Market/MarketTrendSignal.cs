using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Market;

/// <summary>
/// Tier 1: S&P 500 price above both its 50-day and 200-day MA indicates a market uptrend.
/// Per plan: uses OHLCV data for S&P index.
/// </summary>
public sealed class MarketTrendSignal : IMarketSignal
{
    private readonly MarketSignalWeightsOptions _weights;

    public MarketTrendSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.MarketSignalWeights;
    }

    public string Name => "MarketTrend";
    public double DefaultWeight => _weights.MarketTrend;

    public Task<SignalResult> EvaluateAsync(MacroIndicators indicators, IReadOnlyList<OhlcvRecord> spOhlcv)
    {
        if (spOhlcv.Count < 200)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient S&P 500 data", false));

        var closes = spOhlcv.Select(r => (double)r.Close).ToList();
        var currentPrice = closes[^1];
        var ma50 = closes.TakeLast(50).Average();
        var ma200 = closes.TakeLast(200).Average();

        bool aboveBoth = currentPrice > ma50 && currentPrice > ma200;
        bool aboveOnly50 = currentPrice > ma50 && currentPrice <= ma200;
        bool below50 = currentPrice <= ma50;

        if (aboveBoth)
            return Task.FromResult(new SignalResult(Name, 1.0, DefaultWeight,
                $"S&P 500 above both 50-day MA ({ma50:F0}) and 200-day MA ({ma200:F0}) — confirmed uptrend", true));

        if (aboveOnly50)
            return Task.FromResult(new SignalResult(Name, 0.25, DefaultWeight,
                $"S&P 500 above 50-day MA but below 200-day MA — mixed trend signal", true));

        return Task.FromResult(new SignalResult(Name, -1.0, DefaultWeight,
            $"S&P 500 below 50-day MA ({ma50:F0}) — market in downtrend", true));
    }
}
