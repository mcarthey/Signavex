namespace Signavex.Domain.Models;

/// <summary>
/// A news headline associated with a stock ticker.
/// </summary>
public record NewsItem(
    string Ticker,
    string Headline,
    string? Summary,
    string? Source,
    DateTime PublishedAt,
    double? SentimentScore
);
