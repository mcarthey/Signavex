using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Polygon;

namespace Signavex.Infrastructure.Caching;

/// <summary>
/// In-memory cache around PolygonMarketDataProvider. Polygon's free tier caps
/// at 5 requests/minute; each CandidateDetail page load needs OHLCV + profile,
/// so unique tickers alone can saturate the budget. This cache means repeat
/// views of the same ticker cost zero Polygon calls.
///
/// TTLs chosen for the use case:
///   - OHLCV (daily bars): 15 min. Intraday freshness doesn't matter for a
///     multi-week chart; 15 min balances "somewhat fresh" with "not hammering
///     the API".
///   - Ticker profile: 24 hours. Company name/sector/industry/market cap
///     change slowly; 1 day is plenty current.
///   - Index constituents: no cache (the embedded JSON is already in-process).
/// </summary>
public sealed class CachedMarketDataProvider : IMarketDataProvider
{
    private readonly PolygonMarketDataProvider _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedMarketDataProvider> _logger;

    private static readonly TimeSpan OhlcvTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ProfileTtl = TimeSpan.FromHours(24);

    public CachedMarketDataProvider(
        PolygonMarketDataProvider inner,
        IMemoryCache cache,
        ILogger<CachedMarketDataProvider> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<OhlcvRecord>> GetDailyOhlcvAsync(string ticker, int days)
    {
        var key = $"ohlcv:{ticker.ToUpperInvariant()}:{days}";

        if (_cache.TryGetValue<IReadOnlyList<OhlcvRecord>>(key, out var cached) && cached is not null)
        {
            _logger.LogDebug("OHLCV cache hit for {Ticker} ({Days}d)", ticker, days);
            return cached;
        }

        var fresh = (await _inner.GetDailyOhlcvAsync(ticker, days)).ToList();

        // Only cache non-empty results — we don't want a transient rate-limit
        // failure to poison the cache with an empty list for 15 minutes.
        if (fresh.Count > 0)
        {
            _cache.Set(key, (IReadOnlyList<OhlcvRecord>)fresh, OhlcvTtl);
        }

        return fresh;
    }

    public async Task<TickerProfile?> GetTickerProfileAsync(string ticker)
    {
        var key = $"profile:{ticker.ToUpperInvariant()}";

        if (_cache.TryGetValue<TickerProfile>(key, out var cached) && cached is not null)
        {
            _logger.LogDebug("Profile cache hit for {Ticker}", ticker);
            return cached;
        }

        var fresh = await _inner.GetTickerProfileAsync(ticker);

        if (fresh is not null)
        {
            _cache.Set(key, fresh, ProfileTtl);
        }

        return fresh;
    }

    // Index constituents come from embedded JSON — already in-process, no cache needed.
    public Task<IEnumerable<string>> GetIndexConstituentsAsync(MarketIndex index)
        => _inner.GetIndexConstituentsAsync(index);
}
