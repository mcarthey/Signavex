using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Persistence.Entities;

namespace Signavex.Infrastructure.Persistence;

public class SqliteScanStateStore : IScanStateStore
{
    private readonly IDbContextFactory<SignavexDbContext> _dbFactory;
    private readonly ILogger<SqliteScanStateStore> _logger;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public SqliteScanStateStore(
        IDbContextFactory<SignavexDbContext> dbFactory,
        ILogger<SqliteScanStateStore> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task SaveCheckpointAsync(ScanCheckpoint checkpoint, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var payload = JsonSerializer.Serialize(checkpoint, JsonOptions);
        var existing = await db.ScanCheckpoints.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            existing.ScanId = checkpoint.ScanId;
            existing.StartedAtUtc = checkpoint.StartedAtUtc;
            existing.PayloadJson = payload;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            db.ScanCheckpoints.Add(new ScanCheckpointEntity
            {
                ScanId = checkpoint.ScanId,
                StartedAtUtc = checkpoint.StartedAtUtc,
                PayloadJson = payload,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Saved checkpoint: {Evaluated}/{Total} evaluated",
            checkpoint.EvaluatedTickers.Count, checkpoint.UniverseTickers.Count);
    }

    public async Task<ScanCheckpoint?> LoadCheckpointAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.ScanCheckpoints.AsNoTracking().OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<ScanCheckpoint>(entity.PayloadJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize checkpoint — data may be corrupted");
            return null;
        }
    }

    public async Task DeleteCheckpointAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.ScanCheckpoints.ExecuteDeleteAsync(ct);
        _logger.LogDebug("Deleted checkpoint");
    }

    public async Task SaveCompletedResultAsync(CompletedScanResult result, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var run = new ScanRunEntity
        {
            ScanId = result.ScanId,
            CompletedAtUtc = result.CompletedAtUtc,
            MarketMultiplier = result.MarketContext.Multiplier,
            MarketSummary = result.MarketContext.Summary,
            MarketSignalsJson = JsonSerializer.Serialize(result.MarketContext.MarketSignals, JsonOptions),
            TotalEvaluated = result.TotalEvaluated,
            ErrorCount = result.ErrorCount,
            CandidateCount = result.Candidates.Count,
            Candidates = result.Candidates.Select(c => new ScanCandidateEntity
            {
                Ticker = c.Ticker,
                CompanyName = c.CompanyName,
                Tier = (int)c.Tier,
                RawScore = c.RawScore,
                FinalScore = c.FinalScore,
                EvaluatedAt = c.EvaluatedAt,
                SignalResultsJson = JsonSerializer.Serialize(c.SignalResults, JsonOptions)
            }).ToList()
        };

        db.ScanRuns.Add(run);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Saved completed scan result: {Count} candidates", result.Candidates.Count);
    }

    public async Task<CompletedScanResult?> LoadLatestResultAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var run = await db.ScanRuns
            .AsNoTracking()
            .Include(r => r.Candidates)
            .OrderByDescending(r => r.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (run is null)
            return null;

        return ToCompletedScanResult(run);
    }

    internal static CompletedScanResult ToCompletedScanResult(ScanRunEntity run)
    {
        var marketSignals = DeserializeSignals(run.MarketSignalsJson);
        var marketContext = new MarketContext(run.MarketMultiplier, run.MarketSummary, marketSignals);

        var candidates = run.Candidates
            .Select(c => new StockCandidate(
                c.Ticker,
                c.CompanyName,
                (MarketTier)c.Tier,
                c.RawScore,
                c.FinalScore,
                DeserializeSignals(c.SignalResultsJson),
                marketContext,
                c.EvaluatedAt))
            .OrderByDescending(c => c.FinalScore)
            .ToList()
            .AsReadOnly();

        return new CompletedScanResult(
            run.ScanId, run.CompletedAtUtc, marketContext,
            candidates, run.TotalEvaluated, run.ErrorCount);
    }

    internal static IReadOnlyList<SignalResult> DeserializeSignals(string json)
    {
        if (string.IsNullOrEmpty(json))
            return Array.Empty<SignalResult>();

        try
        {
            return JsonSerializer.Deserialize<List<SignalResult>>(json, JsonOptions)
                   ?? (IReadOnlyList<SignalResult>)Array.Empty<SignalResult>();
        }
        catch (JsonException)
        {
            return Array.Empty<SignalResult>();
        }
    }
}
