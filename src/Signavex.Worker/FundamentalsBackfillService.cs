using Microsoft.EntityFrameworkCore;
using Signavex.Domain.Interfaces;
using Signavex.Engine;
using Signavex.Infrastructure.Persistence;

namespace Signavex.Worker;

/// <summary>
/// Background service that drip-feeds Alpha Vantage fundamentals requests to populate
/// the cache. Respects the free tier limits: 5 requests/minute, 25 requests/day.
/// Each ticker requires 2 API calls (OVERVIEW + EARNINGS), so we can cache ~12 tickers/day.
/// </summary>
public class FundamentalsBackfillService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FundamentalsBackfillService> _logger;

    private const int MaxCallsPerDay = 24; // 12 tickers × 2 calls, leave 1 call buffer
    private const int DelayBetweenTickersMs = 25_000; // ~2.4/min (well under 5/min limit)
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);

    public FundamentalsBackfillService(
        IServiceScopeFactory scopeFactory,
        ILogger<FundamentalsBackfillService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for app startup to settle
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var filled = await RunBackfillCycleAsync(stoppingToken);
                _logger.LogInformation("Fundamentals backfill cycle complete: {Count} tickers cached", filled);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fundamentals backfill cycle failed");
            }

            // Wait until next UTC day to reset the daily limit
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1).AddHours(1); // 01:00 UTC next day
            var delay = nextRun - now;
            _logger.LogInformation("Next backfill cycle at {Time} UTC ({Delay})", nextRun, delay);

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task<int> RunBackfillCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var universeProvider = scope.ServiceProvider.GetRequiredService<UniverseProvider>();
        var fundamentalsProvider = scope.ServiceProvider.GetRequiredService<IFundamentalsProvider>();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SignavexDbContext>>();

        // Get all universe tickers
        var universe = await universeProvider.GetUniverseAsync();
        var allTickers = universe.Select(u => u.Ticker).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Find tickers that need caching (no entry or expired)
        var cutoff = DateTime.UtcNow - CacheTtl;
        List<string> cachedTickers;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            cachedTickers = await db.FundamentalsCache
                .Where(f => f.RetrievedAtUtc > cutoff)
                .Select(f => f.Ticker)
                .ToListAsync(ct);
        }

        var cachedSet = new HashSet<string>(cachedTickers, StringComparer.OrdinalIgnoreCase);
        var needsCaching = allTickers.Where(t => !cachedSet.Contains(t)).ToList();

        _logger.LogInformation(
            "Fundamentals backfill: {Cached}/{Total} tickers cached, {Needed} need refresh",
            cachedSet.Count, allTickers.Count, needsCaching.Count);

        if (needsCaching.Count == 0)
            return 0;

        // Process up to MaxCallsPerDay/2 tickers (each ticker = 2 AV calls)
        var ticketsThisCycle = needsCaching.Take(MaxCallsPerDay / 2).ToList();
        var filled = 0;

        foreach (var ticker in ticketsThisCycle)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // This calls through CachedFundamentalsProvider → AlphaVantage → saves to DB cache
                var data = await fundamentalsProvider.GetFundamentalsAsync(ticker);

                if (data.PeRatio.HasValue || data.EpsPreviousYear.HasValue || data.AnalystRating is not null)
                {
                    filled++;
                    _logger.LogDebug("Backfilled fundamentals for {Ticker}", ticker);
                }
                else
                {
                    _logger.LogDebug("No meaningful data returned for {Ticker} (rate limited?)", ticker);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backfill fundamentals for {Ticker}", ticker);
            }

            // Rate limit: wait between tickers
            await Task.Delay(DelayBetweenTickersMs, ct);
        }

        return filled;
    }
}
