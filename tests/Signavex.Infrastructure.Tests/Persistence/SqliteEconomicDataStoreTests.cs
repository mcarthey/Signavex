using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Signavex.Domain.Models.Economic;
using Signavex.Infrastructure.Persistence;
using Signavex.Infrastructure.Persistence.Entities;

namespace Signavex.Infrastructure.Tests.Persistence;

public class SqliteEconomicDataStoreTests : IAsyncDisposable
{
    private readonly IDbContextFactory<SignavexDbContext> _factory;
    private readonly SqliteEconomicDataStore _store;

    public SqliteEconomicDataStoreTests()
    {
        var dbName = $"signavex-econ-test-{Guid.NewGuid():N}.db";
        var options = new DbContextOptionsBuilder<SignavexDbContext>()
            .UseSqlite($"Data Source={dbName}")
            .Options;

        _factory = new TestDbContextFactory(options);

        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();

        _store = new SqliteEconomicDataStore(_factory, NullLogger<SqliteEconomicDataStore>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task GetAllSeries_Empty_ReturnsEmptyList()
    {
        var result = await _store.GetAllSeriesAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllSeries_WithSeed_ReturnsSeries()
    {
        await SeedSeriesAsync("UNRATE", true);
        await SeedSeriesAsync("FEDFUNDS", true);

        var result = await _store.GetAllSeriesAsync();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllSeries_EnabledOnly_FiltersDisabled()
    {
        await SeedSeriesAsync("UNRATE", true);
        await SeedSeriesAsync("DGS10", false);

        var result = await _store.GetAllSeriesAsync(enabledOnly: true);
        Assert.Single(result);
        Assert.Equal("UNRATE", result[0].SeriesId);
    }

    [Fact]
    public async Task GetSeriesById_Exists_ReturnsSeries()
    {
        await SeedSeriesAsync("UNRATE", true);

        var result = await _store.GetSeriesByIdAsync("UNRATE");
        Assert.NotNull(result);
        Assert.Equal("UNRATE", result.SeriesId);
    }

    [Fact]
    public async Task GetSeriesById_NotExists_ReturnsNull()
    {
        var result = await _store.GetSeriesByIdAsync("DOES_NOT_EXIST");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertObservations_NewData_Inserts()
    {
        await SeedSeriesAsync("UNRATE", true);

        var observations = new List<EconomicObservation>
        {
            new("UNRATE", new DateOnly(2024, 1, 1), 3.7),
            new("UNRATE", new DateOnly(2024, 2, 1), 3.9),
            new("UNRATE", new DateOnly(2024, 3, 1), 3.8),
        };

        await _store.UpsertObservationsAsync("UNRATE", observations);

        var result = await _store.GetObservationsAsync("UNRATE");
        Assert.Equal(3, result.Count);
        Assert.Equal(3.7, result[0].Value);
    }

    [Fact]
    public async Task UpsertObservations_ExistingData_Updates()
    {
        await SeedSeriesAsync("UNRATE", true);

        var initial = new List<EconomicObservation>
        {
            new("UNRATE", new DateOnly(2024, 1, 1), 3.7),
        };
        await _store.UpsertObservationsAsync("UNRATE", initial);

        var updated = new List<EconomicObservation>
        {
            new("UNRATE", new DateOnly(2024, 1, 1), 3.9), // same date, different value
        };
        await _store.UpsertObservationsAsync("UNRATE", updated);

        var result = await _store.GetObservationsAsync("UNRATE");
        Assert.Single(result);
        Assert.Equal(3.9, result[0].Value);
    }

    [Fact]
    public async Task GetObservations_WithStartDate_FiltersEarlier()
    {
        await SeedSeriesAsync("UNRATE", true);

        var observations = new List<EconomicObservation>
        {
            new("UNRATE", new DateOnly(2024, 1, 1), 3.7),
            new("UNRATE", new DateOnly(2024, 6, 1), 3.9),
            new("UNRATE", new DateOnly(2024, 12, 1), 4.0),
        };
        await _store.UpsertObservationsAsync("UNRATE", observations);

        var result = await _store.GetObservationsAsync("UNRATE", new DateOnly(2024, 6, 1));
        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2024, 6, 1), result[0].Date);
    }

    [Fact]
    public async Task SyncStatus_NewSeries_ReturnsNull()
    {
        var result = await _store.GetSyncStatusAsync("UNRATE");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateSyncTimestamp_CreatesAndUpdates()
    {
        await _store.UpdateSyncTimestampAsync("UNRATE", 100);

        var result = await _store.GetSyncStatusAsync("UNRATE");
        Assert.NotNull(result);
        Assert.Equal("UNRATE", result.SeriesId);
        Assert.Equal(100, result.ObservationCount);

        // Update it
        await _store.UpdateSyncTimestampAsync("UNRATE", 150);

        result = await _store.GetSyncStatusAsync("UNRATE");
        Assert.NotNull(result);
        Assert.Equal(150, result.ObservationCount);
    }

    private async Task SeedSeriesAsync(string seriesId, bool enabled)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.EconomicSeries.Add(new EconomicSeriesEntity
        {
            SeriesId = seriesId,
            Name = seriesId,
            Description = $"Test {seriesId}",
            Frequency = "Monthly",
            Units = "Percent",
            SeasonalAdjustment = "SA",
            IsEnabled = enabled,
            Category = 0
        });
        await db.SaveChangesAsync();
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
