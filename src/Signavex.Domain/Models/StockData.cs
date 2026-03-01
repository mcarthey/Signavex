namespace Signavex.Domain.Models;

/// <summary>
/// All data needed to evaluate a stock's signals in one pass.
/// </summary>
public record StockData(
    string Ticker,
    string CompanyName,
    IReadOnlyList<OhlcvRecord> OhlcvHistory,
    FundamentalsData? Fundamentals,
    IReadOnlyList<NewsItem> RecentNews
);
