using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Persistence.Entities;

namespace Signavex.Infrastructure.Persistence;

public class SqliteScanCommandStore : IScanCommandStore
{
    private readonly IDbContextFactory<SignavexDbContext> _dbFactory;
    private readonly ILogger<SqliteScanCommandStore> _logger;

    public SqliteScanCommandStore(
        IDbContextFactory<SignavexDbContext> dbFactory,
        ILogger<SqliteScanCommandStore> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task EnqueueCommandAsync(string commandType, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        db.ScanCommands.Add(new ScanCommandEntity
        {
            CommandType = commandType,
            RequestedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Enqueued scan command: {CommandType}", commandType);
    }

    public async Task<ScanCommand?> DequeueCommandAsync(CancellationToken ct = default)
    {
        return await DequeueCommandInternalAsync(null, ct);
    }

    public async Task<ScanCommand?> DequeueCommandAsync(string commandType, CancellationToken ct = default)
    {
        return await DequeueCommandInternalAsync(commandType, ct);
    }

    private async Task<ScanCommand?> DequeueCommandInternalAsync(string? commandType, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.ScanCommands.Where(c => c.PickedUpAtUtc == null);
        if (commandType is not null)
            query = query.Where(c => c.CommandType == commandType);

        var entity = await query
            .OrderBy(c => c.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        entity.PickedUpAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Dequeued scan command {Id}: {CommandType}", entity.Id, entity.CommandType);

        return new ScanCommand(
            entity.Id,
            entity.CommandType,
            entity.RequestedAtUtc,
            entity.PickedUpAtUtc,
            entity.CompletedAtUtc);
    }

    public async Task CompleteCommandAsync(int commandId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ScanCommands.FindAsync(new object[] { commandId }, ct);
        if (entity is not null)
        {
            entity.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Completed scan command {Id}", commandId);
        }
    }

    public async Task<bool> HasPendingCommandAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ScanCommands
            .AnyAsync(c => c.PickedUpAtUtc == null, ct);
    }

    public async Task<bool> HasPendingCommandAsync(string commandType, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ScanCommands
            .AnyAsync(c => c.PickedUpAtUtc == null && c.CommandType == commandType, ct);
    }
}
