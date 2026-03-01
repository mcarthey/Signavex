using Signavex.Domain.Models;
using Signavex.Signals.Fundamental;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Fundamental;

public class PeRatioSignalTests
{
    private readonly PeRatioSignal _signal = new(TestSignalOptionsFactory.CreateDefault());

    private static StockData MakeStock(double? pe, double? industryPe) =>
        new("TEST", "Test Corp",
            Array.Empty<OhlcvRecord>(),
            new FundamentalsData("TEST", pe, industryPe, null, null, null, null, null, DateTime.UtcNow),
            Array.Empty<NewsItem>());

    [Fact]
    public async Task NoPeData_ReturnsZeroNotAvailable()
    {
        var stock = MakeStock(null, null);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task NegativePe_ReturnsNegativeHalf()
    {
        var stock = MakeStock(-5.0, 20.0);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(-0.5, result.Score);
        Assert.Contains("Negative P/E", result.Reason);
    }

    [Fact]
    public async Task Undervalued_ReturnsPositiveScore()
    {
        // P/E 10 vs industry 20 → ratio 0.5 → score 1.0
        var stock = MakeStock(10.0, 20.0);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("below", result.Reason);
    }

    [Fact]
    public async Task Overvalued_ReturnsNegativeScore()
    {
        // P/E 30 vs industry 20 → ratio 1.5 → score -1.0
        var stock = MakeStock(30.0, 20.0);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(-1.0, result.Score);
        Assert.Contains("above", result.Reason);
    }
}
