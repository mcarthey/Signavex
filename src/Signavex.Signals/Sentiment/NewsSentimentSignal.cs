using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Sentiment;

/// <summary>
/// RAT p.163–168 — Positive catalyst present; checks that news isn't already priced in
/// by looking at recent price movement relative to the news date.
/// </summary>
public sealed class NewsSentimentSignal : IStockSignal
{
    private const int NewsDays = 3;
    private const double AlreadyPricedInThreshold = 0.05;

    private readonly SignalWeightsOptions _weights;

    public NewsSentimentSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "NewsSentiment";
    public double DefaultWeight => _weights.NewsSentiment;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        var recentNews = stock.RecentNews
            .Where(n => n.PublishedAt >= DateTime.UtcNow.AddDays(-NewsDays))
            .ToList();

        if (recentNews.Count == 0)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight,
                "No recent news in the last 3 days", true));

        var sentimentedNews = recentNews.Where(n => n.SentimentScore.HasValue).ToList();
        if (sentimentedNews.Count == 0)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight,
                $"{recentNews.Count} recent news items, but no sentiment scores available", false));

        double avgSentiment = sentimentedNews.Average(n => n.SentimentScore!.Value);

        // RAT p.163-168: Check if news is already "in the stock"
        // If price already ran up significantly, discount the news signal
        if (stock.OhlcvHistory.Count >= 5)
        {
            var priceNow = (double)stock.OhlcvHistory[^1].Close;
            var priceFiveDaysAgo = (double)stock.OhlcvHistory[^5].Close;
            double priceChange = (priceNow - priceFiveDaysAgo) / priceFiveDaysAgo;

            if (avgSentiment > 0 && priceChange > AlreadyPricedInThreshold)
            {
                return Task.FromResult(new SignalResult(Name, avgSentiment * 0.3, DefaultWeight,
                    $"Positive news (sentiment {avgSentiment:F2}) but stock already up {priceChange:P1} — may be priced in", true));
            }
        }

        double score = Math.Max(-1.0, Math.Min(1.0, avgSentiment));
        string headline = recentNews.OrderByDescending(n => n.PublishedAt).First().Headline;
        return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
            $"Avg sentiment {avgSentiment:F2} across {sentimentedNews.Count} items. Latest: \"{headline}\"", true));
    }
}
