using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Persistence.Entities;

namespace Signavex.Infrastructure.Persistence;

public class SqliteDailyBriefStore : IDailyBriefStore
{
    private readonly IDbContextFactory<SignavexDbContext> _dbFactory;
    private readonly ILogger<SqliteDailyBriefStore> _logger;

    public SqliteDailyBriefStore(
        IDbContextFactory<SignavexDbContext> dbFactory,
        ILogger<SqliteDailyBriefStore> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task SaveBriefAsync(DailyBrief brief, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.DailyBriefs
            .FirstOrDefaultAsync(b => b.Date == brief.Date, ct);

        if (existing is not null)
        {
            existing.Title = brief.Title;
            existing.Content = brief.Content;
            existing.GeneratedAtUtc = brief.GeneratedAtUtc;
            existing.ScanId = brief.ScanId;
            existing.EconomicHealthScore = brief.EconomicHealthScore;
            existing.MarketOutlook = brief.MarketOutlook;
            existing.CandidateCount = brief.CandidateCount;
        }
        else
        {
            db.DailyBriefs.Add(new DailyBriefEntity
            {
                Date = brief.Date,
                Title = brief.Title,
                Content = brief.Content,
                GeneratedAtUtc = brief.GeneratedAtUtc,
                ScanId = brief.ScanId,
                EconomicHealthScore = brief.EconomicHealthScore,
                MarketOutlook = brief.MarketOutlook,
                CandidateCount = brief.CandidateCount
            });
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Saved daily brief for {Date}", brief.Date);
    }

    public async Task<DailyBrief?> GetLatestBriefAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DailyBriefs.AsNoTracking()
            .OrderByDescending(b => b.Date)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<DailyBrief?> GetBriefByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DailyBriefs.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Date == date, ct);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<DailyBrief>> GetRecentBriefsAsync(int count = 30, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entities = await db.DailyBriefs.AsNoTracking()
            .OrderByDescending(b => b.Date)
            .Take(count)
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList().AsReadOnly();
    }

    private static DailyBrief ToModel(DailyBriefEntity entity) =>
        new(entity.Id, entity.Date, entity.Title, entity.Content,
            entity.GeneratedAtUtc, entity.ScanId, entity.EconomicHealthScore,
            entity.MarketOutlook, entity.CandidateCount);
}
