using Signavex.Signals.Technical;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Technical;

public class SupportResistanceSignalTests
{
    private readonly SupportResistanceSignal _signal = new(TestSignalOptionsFactory.CreateDefault());

    [Fact]
    public async Task InsufficientData_ReturnsZeroScoreNotAvailable()
    {
        var stock = new StockDataBuilder().WithFlatPrices(15, 100m).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task Breakout_ReturnsPositiveOne()
    {
        var builder = new StockDataBuilder();
        // 20 days oscillating with high of 105 — this becomes resistance
        for (int i = 0; i < 20; i++)
        {
            builder.WithDay(100m, 105m, 95m, 100m, 1_000_000);
        }
        // Previous close at resistance, new close breaks above
        builder.WithDay(105m, 110m, 104m, 108m, 1_500_000);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("Breakout", result.Reason);
    }

    [Fact]
    public async Task NearSupport_ReturnsPositiveScore()
    {
        var builder = new StockDataBuilder();
        // 20 days with low of 95 — this becomes support
        for (int i = 0; i < 20; i++)
        {
            builder.WithDay(100m, 105m, 95m, 100m, 1_000_000);
        }
        // Current close near support (within 3%)
        builder.WithDay(96m, 97m, 95m, 95.50m, 1_000_000);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0.75, result.Score);
        Assert.Contains("support", result.Reason);
    }

    [Fact]
    public async Task NearResistance_ReturnsNegativeScore()
    {
        var builder = new StockDataBuilder();
        // 20 days with high of 105 — this becomes resistance
        for (int i = 0; i < 20; i++)
        {
            builder.WithDay(100m, 105m, 95m, 100m, 1_000_000);
        }
        // Current close near resistance (within 3%) but NOT breaking above
        builder.WithDay(103m, 104m, 102m, 103m, 1_000_000);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(-0.25, result.Score);
        Assert.Contains("resistance", result.Reason);
    }

    [Fact]
    public async Task MidRange_ReturnsZeroScore()
    {
        var builder = new StockDataBuilder();
        // 20 days with support at 90, resistance at 110
        for (int i = 0; i < 20; i++)
        {
            builder.WithDay(100m, 110m, 90m, 100m, 1_000_000);
        }
        // Current close in the middle of the range
        builder.WithDay(100m, 101m, 99m, 100m, 1_000_000);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.Contains("Mid-range", result.Reason);
    }
}
