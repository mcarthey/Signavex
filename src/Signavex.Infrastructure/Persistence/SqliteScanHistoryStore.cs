using Microsoft.EntityFrameworkCore;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Infrastructure.Persistence;

public class SqliteScanHistoryStore : IScanHistoryStore
{
    private readonly IDbContextFactory<SignavexDbContext> _dbFactory;

    public SqliteScanHistoryStore(IDbContextFactory<SignavexDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<ScanSummary>> GetRecentScansAsync(int count = 30, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.ScanRuns
            .AsNoTracking()
            .OrderByDescending(r => r.CompletedAtUtc)
            .Take(count)
            .Select(r => new ScanSummary(
                r.ScanId,
                r.CompletedAtUtc,
                r.MarketMultiplier,
                r.MarketSummary,
                r.CandidateCount,
                r.TotalEvaluated,
                r.ErrorCount))
            .ToListAsync(ct);
    }

    public async Task<CompletedScanResult?> GetScanByIdAsync(string scanId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var run = await db.ScanRuns
            .AsNoTracking()
            .Include(r => r.Candidates)
            .FirstOrDefaultAsync(r => r.ScanId == scanId, ct);

        if (run is null)
            return null;

        return SqliteScanStateStore.ToCompletedScanResult(run);
    }

    public async Task<TickerHistory?> GetTickerHistoryAsync(string ticker, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.ScanCandidates
            .AsNoTracking()
            .Where(c => c.Ticker == ticker)
            .Select(c => new
            {
                c.ScanRun.ScanId,
                c.ScanRun.CompletedAtUtc,
                c.RawScore,
                c.FinalScore,
                c.ScanRun.MarketMultiplier,
                c.CompanyName,
                c.EvaluatedAt
            })
            .OrderByDescending(x => x.CompletedAtUtc)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return null;

        var appearances = rows
            .Select(x => new TickerAppearance(x.ScanId, x.CompletedAtUtc, x.RawScore, x.FinalScore, x.MarketMultiplier))
            .ToList();

        var companyName = rows.OrderByDescending(x => x.EvaluatedAt).First().CompanyName;

        return new TickerHistory(ticker, companyName, appearances);
    }

    public async Task<IReadOnlyList<(DateTime Date, double Multiplier)>> GetMarketMultiplierTrendAsync(
        int days = 30, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var results = await db.ScanRuns
            .AsNoTracking()
            .Where(r => r.CompletedAtUtc >= cutoff)
            .OrderBy(r => r.CompletedAtUtc)
            .Select(r => new { r.CompletedAtUtc, r.MarketMultiplier })
            .ToListAsync(ct);

        return results
            .Select(r => (r.CompletedAtUtc, r.MarketMultiplier))
            .ToList()
            .AsReadOnly();
    }
}
