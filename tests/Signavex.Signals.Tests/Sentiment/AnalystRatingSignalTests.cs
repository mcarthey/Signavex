using Signavex.Domain.Models;
using Signavex.Signals.Sentiment;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Sentiment;

public class AnalystRatingSignalTests
{
    private readonly AnalystRatingSignal _signal = new(TestSignalOptionsFactory.CreateDefault());

    private static StockData MakeStock(string? rating) =>
        new("TEST", "Test Corp",
            Array.Empty<OhlcvRecord>(),
            new FundamentalsData("TEST", null, null, null, null, null, null, rating, DateTime.UtcNow),
            Array.Empty<NewsItem>());

    [Fact]
    public async Task NoRating_ReturnsZeroNotAvailable()
    {
        var stock = MakeStock(null);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task StrongBuy_ReturnsPositiveOne()
    {
        var stock = MakeStock("Strong Buy");
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("Strong Buy", result.Reason);
    }

    [Fact]
    public async Task Hold_ReturnsZero()
    {
        var stock = MakeStock("Hold");
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0.0, result.Score);
        Assert.True(result.IsAvailable);
    }

    [Fact]
    public async Task UnrecognizedRating_ReturnsZeroNotAvailable()
    {
        var stock = MakeStock("FooBar Rating");
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
        Assert.Contains("Unrecognized", result.Reason);
    }
}
