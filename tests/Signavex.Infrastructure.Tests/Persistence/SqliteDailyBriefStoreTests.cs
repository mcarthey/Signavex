using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Persistence;

namespace Signavex.Infrastructure.Tests.Persistence;

public class SqliteDailyBriefStoreTests : IAsyncDisposable
{
    private readonly IDbContextFactory<SignavexDbContext> _factory;
    private readonly SqliteDailyBriefStore _store;

    public SqliteDailyBriefStoreTests()
    {
        var dbName = $"signavex-brief-test-{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<SignavexDbContext>()
            .UseSqlite($"Data Source={dbName}")
            .Options;

        _factory = new TestDbContextFactory(options);

        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();

        _store = new SqliteDailyBriefStore(_factory, NullLogger<SqliteDailyBriefStore>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task GetLatestBrief_Empty_ReturnsNull()
    {
        var result = await _store.GetLatestBriefAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveBrief_NewBrief_Persists()
    {
        var brief = CreateBrief(new DateOnly(2025, 3, 1), "Test Brief");

        await _store.SaveBriefAsync(brief);

        var result = await _store.GetLatestBriefAsync();
        Assert.NotNull(result);
        Assert.Equal("Test Brief", result.Title);
        Assert.Equal(new DateOnly(2025, 3, 1), result.Date);
    }

    [Fact]
    public async Task SaveBrief_SameDate_Upserts()
    {
        var date = new DateOnly(2025, 3, 1);
        await _store.SaveBriefAsync(CreateBrief(date, "First Version"));
        await _store.SaveBriefAsync(CreateBrief(date, "Updated Version"));

        var result = await _store.GetBriefByDateAsync(date);
        Assert.NotNull(result);
        Assert.Equal("Updated Version", result.Title);

        // Should only have one record for this date
        var all = await _store.GetRecentBriefsAsync(10);
        Assert.Single(all);
    }

    [Fact]
    public async Task GetBriefByDate_Exists_ReturnsBrief()
    {
        var date = new DateOnly(2025, 3, 1);
        await _store.SaveBriefAsync(CreateBrief(date, "March 1 Brief"));

        var result = await _store.GetBriefByDateAsync(date);
        Assert.NotNull(result);
        Assert.Equal("March 1 Brief", result.Title);
    }

    [Fact]
    public async Task GetBriefByDate_NotExists_ReturnsNull()
    {
        var result = await _store.GetBriefByDateAsync(new DateOnly(2025, 12, 25));
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestBrief_MultipleDates_ReturnsNewest()
    {
        await _store.SaveBriefAsync(CreateBrief(new DateOnly(2025, 3, 1), "March 1"));
        await _store.SaveBriefAsync(CreateBrief(new DateOnly(2025, 3, 3), "March 3"));
        await _store.SaveBriefAsync(CreateBrief(new DateOnly(2025, 3, 2), "March 2"));

        var result = await _store.GetLatestBriefAsync();
        Assert.NotNull(result);
        Assert.Equal("March 3", result.Title);
    }

    [Fact]
    public async Task GetRecentBriefs_ReturnsOrderedByDateDesc()
    {
        await _store.SaveBriefAsync(CreateBrief(new DateOnly(2025, 3, 1), "Day 1"));
        await _store.SaveBriefAsync(CreateBrief(new DateOnly(2025, 3, 2), "Day 2"));
        await _store.SaveBriefAsync(CreateBrief(new DateOnly(2025, 3, 3), "Day 3"));

        var results = await _store.GetRecentBriefsAsync(10);
        Assert.Equal(3, results.Count);
        Assert.Equal("Day 3", results[0].Title);
        Assert.Equal("Day 2", results[1].Title);
        Assert.Equal("Day 1", results[2].Title);
    }

    [Fact]
    public async Task GetRecentBriefs_RespectsCount()
    {
        for (var i = 1; i <= 5; i++)
            await _store.SaveBriefAsync(CreateBrief(new DateOnly(2025, 3, i), $"Day {i}"));

        var results = await _store.GetRecentBriefsAsync(2);
        Assert.Equal(2, results.Count);
        Assert.Equal("Day 5", results[0].Title);
        Assert.Equal("Day 4", results[1].Title);
    }

    [Fact]
    public async Task SaveBrief_PreservesAllFields()
    {
        var brief = new DailyBrief(
            Id: 0,
            Date: new DateOnly(2025, 3, 1),
            Title: "Full Brief",
            Content: "# Headline\nSome content here",
            GeneratedAtUtc: new DateTime(2025, 3, 1, 22, 10, 0, DateTimeKind.Utc),
            ScanId: "scan-123",
            EconomicHealthScore: 72,
            MarketOutlook: "Good",
            CandidateCount: 5);

        await _store.SaveBriefAsync(brief);

        var result = await _store.GetLatestBriefAsync();
        Assert.NotNull(result);
        Assert.Equal("Full Brief", result.Title);
        Assert.Equal("# Headline\nSome content here", result.Content);
        Assert.Equal("scan-123", result.ScanId);
        Assert.Equal(72, result.EconomicHealthScore);
        Assert.Equal("Good", result.MarketOutlook);
        Assert.Equal(5, result.CandidateCount);
    }

    private static DailyBrief CreateBrief(DateOnly date, string title) =>
        new(Id: 0, Date: date, Title: title,
            Content: $"# {title}\nContent for {date}",
            GeneratedAtUtc: DateTime.UtcNow,
            ScanId: null, EconomicHealthScore: 65,
            MarketOutlook: "Fair", CandidateCount: 0);

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
