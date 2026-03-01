using Signavex.Signals.Technical;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Technical;

public class MovingAverageCrossoverSignalTests
{
    private readonly MovingAverageCrossoverSignal _signal = new(TestSignalOptionsFactory.CreateDefault());

    [Fact]
    public async Task InsufficientData_ReturnsZeroScoreNotAvailable()
    {
        var stock = new StockDataBuilder().WithFlatPrices(20, 100m).Build();

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task BullishCrossover_ReturnsPositiveOne()
    {
        // Build 30 days of declining prices (14-day MA below 30-day MA),
        // then a sharp jump so yesterday's 14-day MA <= 30-day MA but today's crosses above.
        var builder = new StockDataBuilder();
        // 30 days at base price — establishes a 30-day MA around 50
        builder.WithFlatPrices(30, 50m);
        // Drop slightly to push 14-day MA below 30-day MA
        builder.WithFlatPrices(14, 48m);
        // Jump up sharply to force the crossover on the last day
        builder.WithDay(60m, 60m, 60m, 60m, 1_000_000);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("Bullish crossover", result.Reason);
    }

    [Fact]
    public async Task BearishCrossover_ReturnsNegativeOne()
    {
        var builder = new StockDataBuilder();
        // 30 days at base price
        builder.WithFlatPrices(30, 50m);
        // Rise to push 14-day MA above 30-day MA
        builder.WithFlatPrices(14, 52m);
        // Drop sharply to force the bearish crossover on the last day
        builder.WithDay(38m, 38m, 38m, 38m, 1_000_000);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(-1.0, result.Score);
        Assert.Contains("Bearish crossover", result.Reason);
    }

    [Fact]
    public async Task AboveMA_ReturnsHalfPoint()
    {
        var builder = new StockDataBuilder();
        // Steadily rising prices — 14-day MA stays above 30-day MA without a fresh crossover
        builder.WithTrendingPrices(50, 40m, 60m);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0.5, result.Score);
        Assert.Contains("uptrend", result.Reason);
    }

    [Fact]
    public async Task BelowMA_ReturnsNegativeHalfPoint()
    {
        var builder = new StockDataBuilder();
        // Steadily declining prices — 14-day MA stays below 30-day MA
        builder.WithTrendingPrices(50, 60m, 40m);

        var stock = builder.Build();
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(-0.5, result.Score);
        Assert.Contains("downtrend", result.Reason);
    }
}
