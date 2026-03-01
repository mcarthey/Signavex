using Microsoft.EntityFrameworkCore;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Persistence;
using Signavex.Infrastructure.Persistence.Entities;

namespace Signavex.Infrastructure.Tests.Persistence;

public class SqliteScanHistoryStoreTests : IAsyncDisposable
{
    private readonly IDbContextFactory<SignavexDbContext> _factory;
    private readonly SqliteScanHistoryStore _store;

    public SqliteScanHistoryStoreTests()
    {
        var dbName = $"signavex-history-test-{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<SignavexDbContext>()
            .UseSqlite($"Data Source={dbName}")
            .Options;

        _factory = new TestDbContextFactory(options);

        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();

        _store = new SqliteScanHistoryStore(_factory);
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    private async Task SeedScanRunAsync(string scanId, DateTime completedAt, double multiplier, int candidateCount,
        params (string Ticker, double FinalScore)[] candidates)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var run = new ScanRunEntity
        {
            ScanId = scanId,
            CompletedAtUtc = completedAt,
            MarketMultiplier = multiplier,
            MarketSummary = multiplier >= 1.0 ? "Bullish" : "Bearish",
            MarketSignalsJson = "[]",
            TotalEvaluated = 500,
            ErrorCount = 0,
            CandidateCount = candidateCount,
            Candidates = candidates.Select(c => new ScanCandidateEntity
            {
                Ticker = c.Ticker,
                CompanyName = $"{c.Ticker} Inc",
                Tier = 1,
                RawScore = c.FinalScore * 0.9,
                FinalScore = c.FinalScore,
                EvaluatedAt = completedAt,
                SignalResultsJson = "[]"
            }).ToList()
        };

        db.ScanRuns.Add(run);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetRecentScans_ReturnsOrderedByDate()
    {
        await SeedScanRunAsync("old", DateTime.UtcNow.AddDays(-2), 0.9, 3);
        await SeedScanRunAsync("mid", DateTime.UtcNow.AddDays(-1), 1.1, 5);
        await SeedScanRunAsync("new", DateTime.UtcNow, 1.0, 4);

        var scans = await _store.GetRecentScansAsync();

        Assert.Equal(3, scans.Count);
        Assert.Equal("new", scans[0].ScanId);
        Assert.Equal("mid", scans[1].ScanId);
        Assert.Equal("old", scans[2].ScanId);
    }

    [Fact]
    public async Task GetRecentScans_RespectsCount()
    {
        for (int i = 0; i < 5; i++)
            await SeedScanRunAsync($"scan{i}", DateTime.UtcNow.AddHours(-i), 1.0, 1);

        var scans = await _store.GetRecentScansAsync(count: 3);
        Assert.Equal(3, scans.Count);
    }

    [Fact]
    public async Task GetRecentScans_Empty_ReturnsEmptyList()
    {
        var scans = await _store.GetRecentScansAsync();
        Assert.Empty(scans);
    }

    [Fact]
    public async Task GetScanById_ReturnsScanWithCandidates()
    {
        await SeedScanRunAsync("target", DateTime.UtcNow, 1.2, 2, ("AAPL", 0.8), ("MSFT", 0.75));

        var result = await _store.GetScanByIdAsync("target");

        Assert.NotNull(result);
        Assert.Equal("target", result.ScanId);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public async Task GetScanById_NotFound_ReturnsNull()
    {
        var result = await _store.GetScanByIdAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTickerHistory_ReturnsAppearancesAcrossScans()
    {
        await SeedScanRunAsync("s1", DateTime.UtcNow.AddDays(-2), 0.9, 1, ("AAPL", 0.7));
        await SeedScanRunAsync("s2", DateTime.UtcNow.AddDays(-1), 1.0, 2, ("AAPL", 0.8), ("MSFT", 0.6));
        await SeedScanRunAsync("s3", DateTime.UtcNow, 1.1, 1, ("AAPL", 0.85));

        var history = await _store.GetTickerHistoryAsync("AAPL");

        Assert.NotNull(history);
        Assert.Equal("AAPL", history.Ticker);
        Assert.Equal("AAPL Inc", history.CompanyName);
        Assert.Equal(3, history.Appearances.Count);
        // Most recent first
        Assert.Equal("s3", history.Appearances[0].ScanId);
    }

    [Fact]
    public async Task GetTickerHistory_NotFound_ReturnsNull()
    {
        var result = await _store.GetTickerHistoryAsync("ZZZZ");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMarketMultiplierTrend_ReturnsChronological()
    {
        await SeedScanRunAsync("a", DateTime.UtcNow.AddDays(-3), 0.8, 0);
        await SeedScanRunAsync("b", DateTime.UtcNow.AddDays(-2), 1.0, 0);
        await SeedScanRunAsync("c", DateTime.UtcNow.AddDays(-1), 1.2, 0);

        var trend = await _store.GetMarketMultiplierTrendAsync(days: 30);

        Assert.Equal(3, trend.Count);
        Assert.Equal(0.8, trend[0].Multiplier);
        Assert.Equal(1.0, trend[1].Multiplier);
        Assert.Equal(1.2, trend[2].Multiplier);
    }

    [Fact]
    public async Task GetMarketMultiplierTrend_RespectsDateFilter()
    {
        await SeedScanRunAsync("old", DateTime.UtcNow.AddDays(-60), 0.7, 0);
        await SeedScanRunAsync("recent", DateTime.UtcNow.AddDays(-5), 1.1, 0);

        var trend = await _store.GetMarketMultiplierTrendAsync(days: 30);

        Assert.Single(trend);
        Assert.Equal(1.1, trend[0].Multiplier);
    }

    private class TestDbContextFactory : IDbContextFactory<SignavexDbContext>
    {
        private readonly DbContextOptions<SignavexDbContext> _options;

        public TestDbContextFactory(DbContextOptions<SignavexDbContext> options)
        {
            _options = options;
        }

        public SignavexDbContext CreateDbContext() => new(_options);
    }
}
