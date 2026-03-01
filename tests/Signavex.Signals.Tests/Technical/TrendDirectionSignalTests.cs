using Signavex.Signals.Technical;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Technical;

public class TrendDirectionSignalTests
{
    private readonly TrendDirectionSignal _signal = new(TestSignalOptionsFactory.CreateDefault());

    [Fact]
    public async Task InsufficientData_ReturnsZeroScoreNotAvailable()
    {
        var stock = new StockDataBuilder().WithFlatPrices(10, 100m).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task StrongUptrend_ReturnsPositiveOne()
    {
        // Strong upward slope: normalized slope > 0.005
        var stock = new StockDataBuilder().WithTrendingPrices(25, 80m, 120m).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("upward", result.Reason);
    }

    [Fact]
    public async Task StrongDowntrend_ReturnsNegativeOne()
    {
        // Strong downward slope: normalized slope < -0.005
        var stock = new StockDataBuilder().WithTrendingPrices(25, 120m, 80m).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(-1.0, result.Score);
        Assert.Contains("downward", result.Reason);
    }

    [Fact]
    public async Task WeakUptrend_ReturnsModestScore()
    {
        // Mild upward slope: normalized slope between 0 and 0.005
        var stock = new StockDataBuilder().WithTrendingPrices(25, 100m, 103m).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.True(result.Score > 0, "Score should be positive for uptrend");
        Assert.True(result.Score < 1.0, "Score should be modest for weak uptrend");
        Assert.Contains("upward", result.Reason);
    }
}
