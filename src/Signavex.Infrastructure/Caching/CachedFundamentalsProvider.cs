using Microsoft.Extensions.Caching.Memory;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Infrastructure.Caching;

/// <summary>
/// Caching decorator for IFundamentalsProvider. Fundamental data refreshes quarterly,
/// so we cache aggressively with a 24-hour sliding expiration.
/// </summary>
public sealed class CachedFundamentalsProvider : IFundamentalsProvider
{
    private readonly IFundamentalsProvider _inner;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public CachedFundamentalsProvider(IFundamentalsProvider inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<FundamentalsData> GetFundamentalsAsync(string ticker)
    {
        var cacheKey = $"fundamentals:{ticker}";

        if (_cache.TryGetValue(cacheKey, out FundamentalsData? cached) && cached is not null)
            return cached;

        var data = await _inner.GetFundamentalsAsync(ticker);

        _cache.Set(cacheKey, data, new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheDuration
        });

        return data;
    }
}
