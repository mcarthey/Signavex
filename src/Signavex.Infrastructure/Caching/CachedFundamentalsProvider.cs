using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Persistence;
using Signavex.Infrastructure.Persistence.Entities;

namespace Signavex.Infrastructure.Caching;

/// <summary>
/// Caching decorator for IFundamentalsProvider with two layers:
/// 1. In-memory cache (24h sliding) — fast, lost on restart
/// 2. SQLite/SQL Server cache (7-day TTL) — survives restarts, accumulates over scans
///
/// With Alpha Vantage free tier (25 calls/day), fundamentals data accumulates gradually
/// across multiple scan runs. Balance sheets and earnings change quarterly, so 7-day
/// staleness is perfectly acceptable.
/// </summary>
public sealed class CachedFundamentalsProvider : IFundamentalsProvider
{
    private readonly IFundamentalsProvider _inner;
    private readonly IMemoryCache _memoryCache;
    private readonly IDbContextFactory<SignavexDbContext> _dbFactory;
    private readonly ILogger<CachedFundamentalsProvider> _logger;

    private static readonly TimeSpan MemoryCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan DbCacheTtl = TimeSpan.FromDays(7);

    public CachedFundamentalsProvider(
        IFundamentalsProvider inner,
        IMemoryCache memoryCache,
        IDbContextFactory<SignavexDbContext> dbFactory,
        ILogger<CachedFundamentalsProvider> logger)
    {
        _inner = inner;
        _memoryCache = memoryCache;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<FundamentalsData> GetFundamentalsAsync(string ticker)
    {
        var cacheKey = $"fundamentals:{ticker}";

        // Layer 1: in-memory cache
        if (_memoryCache.TryGetValue(cacheKey, out FundamentalsData? memoryCached) && memoryCached is not null)
            return memoryCached;

        // Layer 2: database cache
        var dbCached = await GetFromDbCacheAsync(ticker);
        if (dbCached is not null)
        {
            _memoryCache.Set(cacheKey, dbCached, new MemoryCacheEntryOptions
            {
                SlidingExpiration = MemoryCacheDuration
            });
            return dbCached;
        }

        // Layer 3: live API call
        var data = await _inner.GetFundamentalsAsync(ticker);

        // Only cache if we got meaningful data (at least one non-null field)
        if (HasMeaningfulData(data))
        {
            await SaveToDbCacheAsync(data);
            _memoryCache.Set(cacheKey, data, new MemoryCacheEntryOptions
            {
                SlidingExpiration = MemoryCacheDuration
            });
        }

        return data;
    }

    private async Task<FundamentalsData?> GetFromDbCacheAsync(string ticker)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var cutoff = DateTime.UtcNow - DbCacheTtl;

            var entity = await db.FundamentalsCache
                .AsNoTracking()
                .Where(f => f.Ticker == ticker && f.RetrievedAtUtc > cutoff)
                .FirstOrDefaultAsync();

            if (entity is null)
                return null;

            return new FundamentalsData(
                Ticker: entity.Ticker,
                PeRatio: entity.PeRatio,
                IndustryPeRatio: entity.IndustryPeRatio,
                DebtToEquityRatio: entity.DebtToEquityRatio,
                EpsCurrentQuarter: entity.EpsCurrentQuarter,
                EpsEstimateCurrentQuarter: entity.EpsEstimateCurrentQuarter,
                EpsPreviousYear: entity.EpsPreviousYear,
                AnalystRating: entity.AnalystRating,
                RetrievedAt: entity.RetrievedAtUtc
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read fundamentals cache for {Ticker}", ticker);
            return null;
        }
    }

    private async Task SaveToDbCacheAsync(FundamentalsData data)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var existing = await db.FundamentalsCache
                .FirstOrDefaultAsync(f => f.Ticker == data.Ticker);

            if (existing is not null)
            {
                existing.PeRatio = data.PeRatio;
                existing.IndustryPeRatio = data.IndustryPeRatio;
                existing.DebtToEquityRatio = data.DebtToEquityRatio;
                existing.EpsCurrentQuarter = data.EpsCurrentQuarter;
                existing.EpsEstimateCurrentQuarter = data.EpsEstimateCurrentQuarter;
                existing.EpsPreviousYear = data.EpsPreviousYear;
                existing.AnalystRating = data.AnalystRating;
                existing.RetrievedAtUtc = data.RetrievedAt;
            }
            else
            {
                db.FundamentalsCache.Add(new FundamentalsCacheEntity
                {
                    Ticker = data.Ticker,
                    PeRatio = data.PeRatio,
                    IndustryPeRatio = data.IndustryPeRatio,
                    DebtToEquityRatio = data.DebtToEquityRatio,
                    EpsCurrentQuarter = data.EpsCurrentQuarter,
                    EpsEstimateCurrentQuarter = data.EpsEstimateCurrentQuarter,
                    EpsPreviousYear = data.EpsPreviousYear,
                    AnalystRating = data.AnalystRating,
                    RetrievedAtUtc = data.RetrievedAt,
                });
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save fundamentals cache for {Ticker}", data.Ticker);
        }
    }

    private static bool HasMeaningfulData(FundamentalsData data)
    {
        return data.PeRatio.HasValue
            || data.DebtToEquityRatio.HasValue
            || data.EpsCurrentQuarter.HasValue
            || data.EpsPreviousYear.HasValue
            || data.AnalystRating is not null;
    }
}
