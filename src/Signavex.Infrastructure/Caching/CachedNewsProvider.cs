using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Polygon;

namespace Signavex.Infrastructure.Caching;

/// <summary>
/// In-memory cache around PolygonNewsProvider. News changes more often than
/// ticker profiles but not minute-to-minute — 30 minutes is plenty current
/// for chart markers and doesn't meaningfully change the user's view.
/// </summary>
public sealed class CachedNewsProvider : INewsDataProvider
{
    private readonly PolygonNewsProvider _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedNewsProvider> _logger;

    private static readonly TimeSpan NewsTtl = TimeSpan.FromMinutes(30);

    public CachedNewsProvider(
        PolygonNewsProvider inner,
        IMemoryCache cache,
        ILogger<CachedNewsProvider> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<NewsItem>> GetRecentNewsAsync(string ticker, int days)
    {
        var key = $"news:{ticker.ToUpperInvariant()}:{days}";

        if (_cache.TryGetValue<IReadOnlyList<NewsItem>>(key, out var cached) && cached is not null)
        {
            _logger.LogDebug("News cache hit for {Ticker} ({Days}d)", ticker, days);
            return cached;
        }

        var fresh = (await _inner.GetRecentNewsAsync(ticker, days)).ToList();

        // Don't cache empty results — avoid poisoning the cache on a transient
        // rate-limit or API failure.
        if (fresh.Count > 0)
        {
            _cache.Set(key, (IReadOnlyList<NewsItem>)fresh, NewsTtl);
        }

        return fresh;
    }
}
