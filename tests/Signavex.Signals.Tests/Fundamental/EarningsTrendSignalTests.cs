using Signavex.Domain.Models;
using Signavex.Signals.Fundamental;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Fundamental;

public class EarningsTrendSignalTests
{
    private readonly EarningsTrendSignal _signal = new(TestSignalOptionsFactory.CreateDefault());

    private static StockData MakeStock(double? eps, double? estimate) =>
        new("TEST", "Test Corp",
            Array.Empty<OhlcvRecord>(),
            new FundamentalsData("TEST", null, null, null, eps, estimate, null, null, DateTime.UtcNow),
            Array.Empty<NewsItem>());

    [Fact]
    public async Task NoData_ReturnsZeroNotAvailable()
    {
        var stock = MakeStock(null, null);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task StrongBeat_ReturnsPositiveOne()
    {
        // EPS 1.20 vs estimate 1.00 → surprise +20% → score 1.0
        var stock = MakeStock(1.20, 1.00);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("beat", result.Reason);
    }

    [Fact]
    public async Task StrongMiss_ReturnsNegativeOne()
    {
        // EPS 0.80 vs estimate 1.00 → surprise -20% → score -1.0
        var stock = MakeStock(0.80, 1.00);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(-1.0, result.Score);
        Assert.Contains("missed", result.Reason);
    }

    [Fact]
    public async Task ZeroEstimate_ReturnsNotAvailable()
    {
        var stock = MakeStock(1.00, 0.0);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }
}
