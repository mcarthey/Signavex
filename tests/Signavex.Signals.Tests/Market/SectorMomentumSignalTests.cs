using Signavex.Domain.Models;
using Signavex.Signals.Market;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Market;

public class SectorMomentumSignalTests
{
    private readonly SectorMomentumSignal _signal = new(TestSignalOptionsFactory.CreateDefault());
    private static readonly MacroIndicators DefaultMacro = new(5.0, 5.0, 18.0, DateTime.UtcNow);

    [Fact]
    public async Task InsufficientData_ReturnsNotAvailable()
    {
        var ohlcv = new StockDataBuilder("XLK").WithFlatPrices(10, 100m).Build().OhlcvHistory;
        var result = await _signal.EvaluateAsync(DefaultMacro, ohlcv);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task StrongUpMomentum_ReturnsPositiveOne()
    {
        // Sector ETF up >5% over 20 days
        var ohlcv = new StockDataBuilder("XLK").WithTrendingPrices(25, 100m, 110m).Build().OhlcvHistory;
        var result = await _signal.EvaluateAsync(DefaultMacro, ohlcv);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("up", result.Reason);
    }

    [Fact]
    public async Task StrongDownMomentum_ReturnsNegativeOne()
    {
        // Sector ETF down >5% over 20 days
        var ohlcv = new StockDataBuilder("XLK").WithTrendingPrices(25, 110m, 100m).Build().OhlcvHistory;
        var result = await _signal.EvaluateAsync(DefaultMacro, ohlcv);

        Assert.Equal(-1.0, result.Score);
        Assert.Contains("down", result.Reason);
    }
}
