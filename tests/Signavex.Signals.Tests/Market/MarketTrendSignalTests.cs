using Signavex.Domain.Models;
using Signavex.Signals.Market;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Market;

public class MarketTrendSignalTests
{
    private readonly MarketTrendSignal _signal = new(TestSignalOptionsFactory.CreateDefault());
    private static readonly MacroIndicators DefaultMacro = new(5.0, 5.0, 18.0, DateTime.UtcNow);

    private static IReadOnlyList<OhlcvRecord> MakeSpOhlcv(int days, decimal price)
    {
        var builder = new StockDataBuilder("SPY");
        builder.WithFlatPrices(days, price);
        return builder.Build().OhlcvHistory;
    }

    [Fact]
    public async Task InsufficientData_ReturnsNotAvailable()
    {
        var ohlcv = MakeSpOhlcv(100, 400m);
        var result = await _signal.EvaluateAsync(DefaultMacro, ohlcv);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task AboveBothMAs_ReturnsPositiveOne()
    {
        // 200 days of trending up — current price above both 50-day and 200-day MAs
        var builder = new StockDataBuilder("SPY");
        builder.WithTrendingPrices(200, 350m, 450m);
        var ohlcv = builder.Build().OhlcvHistory;

        var result = await _signal.EvaluateAsync(DefaultMacro, ohlcv);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("confirmed uptrend", result.Reason);
    }

    [Fact]
    public async Task BelowFiftyDayMA_ReturnsNegativeOne()
    {
        // 200 days trending down — current price below 50-day MA
        var builder = new StockDataBuilder("SPY");
        builder.WithTrendingPrices(200, 450m, 350m);
        var ohlcv = builder.Build().OhlcvHistory;

        var result = await _signal.EvaluateAsync(DefaultMacro, ohlcv);

        Assert.Equal(-1.0, result.Score);
        Assert.Contains("downtrend", result.Reason);
    }
}
