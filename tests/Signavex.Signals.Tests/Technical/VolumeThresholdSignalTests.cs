using Signavex.Signals.Technical;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Technical;

public class VolumeThresholdSignalTests
{
    private readonly VolumeThresholdSignal _signal = new(TestSignalOptionsFactory.CreateDefault());

    [Fact]
    public async Task InsufficientData_ReturnsZeroScoreNotAvailable()
    {
        var stock = new StockDataBuilder().WithFlatPrices(10, 100m).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
        Assert.Contains("Insufficient", result.Reason);
    }

    [Fact]
    public async Task BelowMinimumVolume_ReturnsNegativeScore()
    {
        var stock = new StockDataBuilder().WithFlatPrices(20, 100m, volume: 100_000).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(-0.5, result.Score);
        Assert.Contains("below", result.Reason);
    }

    [Fact]
    public async Task VolumeSpike_ReturnsPositiveScore()
    {
        var builder = new StockDataBuilder();
        // 19 days of normal volume
        builder.WithFlatPrices(19, 100m, volume: 1_000_000);
        // Last day has a volume spike (1.5x+)
        builder.WithDay(100m, 100m, 100m, 100m, 2_000_000);
        var stock = builder.Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("spike", result.Reason);
    }

    [Fact]
    public async Task AdequateVolume_ReturnsModestPositiveScore()
    {
        var stock = new StockDataBuilder().WithFlatPrices(20, 100m, volume: 1_000_000).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0.25, result.Score);
        Assert.True(result.IsAvailable);
    }
}
