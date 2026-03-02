using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models.Economic;
using Signavex.Infrastructure.Persistence.Entities;

namespace Signavex.Infrastructure.Persistence;

public class SqliteEconomicDataStore : IEconomicDataStore
{
    private readonly IDbContextFactory<SignavexDbContext> _dbFactory;
    private readonly ILogger<SqliteEconomicDataStore> _logger;

    public SqliteEconomicDataStore(
        IDbContextFactory<SignavexDbContext> dbFactory,
        ILogger<SqliteEconomicDataStore> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EconomicSeries>> GetAllSeriesAsync(bool enabledOnly = false, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.EconomicSeries.AsNoTracking().AsQueryable();
        if (enabledOnly)
            query = query.Where(s => s.IsEnabled);

        var entities = await query.OrderBy(s => s.Category).ThenBy(s => s.SeriesId).ToListAsync(ct);
        return entities.Select(ToModel).ToList().AsReadOnly();
    }

    public async Task<EconomicSeries?> GetSeriesByIdAsync(string seriesId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.EconomicSeries.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SeriesId == seriesId, ct);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<EconomicObservation>> GetObservationsAsync(
        string seriesId, DateOnly? startDate = null, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.EconomicObservations.AsNoTracking()
            .Where(o => o.SeriesId == seriesId);

        if (startDate.HasValue)
            query = query.Where(o => o.Date >= startDate.Value);

        var entities = await query.OrderBy(o => o.Date).ToListAsync(ct);
        return entities.Select(o => new EconomicObservation(o.SeriesId, o.Date, o.Value))
            .ToList().AsReadOnly();
    }

    public async Task UpsertObservationsAsync(
        string seriesId, IReadOnlyList<EconomicObservation> observations, CancellationToken ct = default)
    {
        if (observations.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var dates = observations.Select(o => o.Date).ToHashSet();
        var existing = await db.EconomicObservations
            .Where(o => o.SeriesId == seriesId && dates.Contains(o.Date))
            .ToDictionaryAsync(o => o.Date, ct);

        foreach (var obs in observations)
        {
            if (existing.TryGetValue(obs.Date, out var entity))
            {
                entity.Value = obs.Value;
            }
            else
            {
                db.EconomicObservations.Add(new EconomicObservationEntity
                {
                    SeriesId = obs.SeriesId,
                    Date = obs.Date,
                    Value = obs.Value
                });
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Upserted {Count} observations for {SeriesId}", observations.Count, seriesId);
    }

    public async Task<EconomicSyncStatus?> GetSyncStatusAsync(string seriesId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.EconomicSyncTrackers.AsNoTracking()
            .FirstOrDefaultAsync(t => t.SeriesId == seriesId, ct);

        return entity is null
            ? null
            : new EconomicSyncStatus(entity.SeriesId, entity.LastSyncUtc, entity.ObservationCount);
    }

    public async Task UpdateSyncTimestampAsync(string seriesId, int observationCount, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.EconomicSyncTrackers
            .FirstOrDefaultAsync(t => t.SeriesId == seriesId, ct);

        if (entity is not null)
        {
            entity.LastSyncUtc = DateTime.UtcNow;
            entity.ObservationCount = observationCount;
        }
        else
        {
            db.EconomicSyncTrackers.Add(new EconomicSyncTrackerEntity
            {
                SeriesId = seriesId,
                LastSyncUtc = DateTime.UtcNow,
                ObservationCount = observationCount
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static EconomicSeries ToModel(EconomicSeriesEntity entity) =>
        new(entity.SeriesId, entity.Name, entity.Description, entity.Frequency,
            entity.Units, entity.SeasonalAdjustment, entity.LastUpdated,
            entity.IsEnabled, (EconomicCategory)entity.Category);
}
