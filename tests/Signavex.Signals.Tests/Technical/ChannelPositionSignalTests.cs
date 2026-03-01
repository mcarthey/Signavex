using Signavex.Signals.Technical;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Technical;

public class ChannelPositionSignalTests
{
    private readonly ChannelPositionSignal _signal = new(TestSignalOptionsFactory.CreateDefault());

    [Fact]
    public async Task InsufficientData_ReturnsZeroScoreNotAvailable()
    {
        var stock = new StockDataBuilder().WithFlatPrices(10, 100m).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task NearBottom_ReturnsPositiveOne()
    {
        var builder = new StockDataBuilder();
        // 19 days with wide range
        for (int i = 0; i < 19; i++)
        {
            builder.WithDay(100m, 110m, 90m, 100m, 1_000_000);
        }
        // Close near bottom of channel (position <= 0.2)
        builder.WithDay(91m, 92m, 90m, 91m, 1_000_000);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("bottom", result.Reason);
    }

    [Fact]
    public async Task MidChannel_ReturnsZeroScore()
    {
        var builder = new StockDataBuilder();
        // 19 days with range 90-110
        for (int i = 0; i < 19; i++)
        {
            builder.WithDay(100m, 110m, 90m, 100m, 1_000_000);
        }
        // Close in middle of channel
        builder.WithDay(100m, 101m, 99m, 100m, 1_000_000);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0.0, result.Score);
        Assert.Contains("mid-channel", result.Reason);
    }

    [Fact]
    public async Task NearTop_ReturnsNegativeOne()
    {
        var builder = new StockDataBuilder();
        // 19 days with range 90-110
        for (int i = 0; i < 19; i++)
        {
            builder.WithDay(100m, 110m, 90m, 100m, 1_000_000);
        }
        // Close near top of channel (position > 0.8)
        builder.WithDay(109m, 110m, 108m, 109m, 1_000_000);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(-1.0, result.Score);
        Assert.Contains("top", result.Reason);
    }

    [Fact]
    public async Task ZeroRange_ReturnsZeroScoreNotAvailable()
    {
        // All OHLC identical — no channel range
        var stock = new StockDataBuilder().WithFlatPrices(20, 100m).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
        Assert.Contains("No meaningful", result.Reason);
    }
}
