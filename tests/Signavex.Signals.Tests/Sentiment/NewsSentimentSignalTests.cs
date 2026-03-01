using Signavex.Domain.Models;
using Signavex.Signals.Sentiment;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Sentiment;

public class NewsSentimentSignalTests
{
    private readonly NewsSentimentSignal _signal = new(TestSignalOptionsFactory.CreateDefault());

    [Fact]
    public async Task NoRecentNews_ReturnsZeroAvailable()
    {
        var stock = new StockData("TEST", "Test Corp",
            Array.Empty<OhlcvRecord>(), null, Array.Empty<NewsItem>());

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.True(result.IsAvailable);
        Assert.Contains("No recent news", result.Reason);
    }

    [Fact]
    public async Task PositiveSentiment_ReturnsPositiveScore()
    {
        var news = new[]
        {
            new NewsItem("TEST", "Great earnings!", null, "Reuters", DateTime.UtcNow.AddHours(-1), 0.8),
            new NewsItem("TEST", "Strong outlook", null, "Bloomberg", DateTime.UtcNow.AddHours(-2), 0.6)
        };
        var stock = new StockData("TEST", "Test Corp",
            Array.Empty<OhlcvRecord>(), null, news);

        var result = await _signal.EvaluateAsync(stock);

        Assert.True(result.Score > 0);
        Assert.True(result.IsAvailable);
    }

    [Fact]
    public async Task PositiveNewsPricedIn_ReturnsDiscountedScore()
    {
        var news = new[]
        {
            new NewsItem("TEST", "Great news!", null, "Reuters", DateTime.UtcNow.AddHours(-1), 0.8)
        };
        // Build OHLCV showing a significant run-up over 5 days
        var builder = new StockDataBuilder();
        builder.WithTrendingPrices(10, 100m, 120m);
        var baseStock = builder.Build();

        var stock = new StockData("TEST", "Test Corp",
            baseStock.OhlcvHistory, null, news);

        var result = await _signal.EvaluateAsync(stock);

        // Score should be discounted (0.8 * 0.3 = 0.24)
        Assert.True(result.Score < 0.5, "Score should be discounted when news is priced in");
        Assert.Contains("priced in", result.Reason);
    }

    [Fact]
    public async Task NoSentimentScores_ReturnsNotAvailable()
    {
        var news = new[]
        {
            new NewsItem("TEST", "Breaking news!", null, "Reuters", DateTime.UtcNow.AddHours(-1), null)
        };
        var stock = new StockData("TEST", "Test Corp",
            Array.Empty<OhlcvRecord>(), null, news);

        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }
}
