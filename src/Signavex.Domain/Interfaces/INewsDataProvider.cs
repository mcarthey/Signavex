using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

/// <summary>
/// Abstraction over news and sentiment data providers (e.g. Polygon.io, NewsAPI).
/// </summary>
public interface INewsDataProvider
{
    Task<IEnumerable<NewsItem>> GetRecentNewsAsync(string ticker, int days);
}
