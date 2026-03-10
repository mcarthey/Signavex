using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Signavex.Infrastructure.Persistence;

/// <summary>
/// One-time transition for SQLite databases created with EnsureCreatedAsync() (no migration history).
/// Preserves all existing data while switching to MigrateAsync().
/// After this runs once, all future startups use normal incremental MigrateAsync().
/// </summary>
public static class SqliteMigrationTransition
{
    public static async Task<bool> TransitionIfNeededAsync(
        string dataDirectory,
        IDbContextFactory<SignavexDbContext> factory,
        ILogger logger)
    {
        var dbPath = Path.Combine(dataDirectory, "signavex.db");

        if (!File.Exists(dbPath))
            return false;

        var state = await DetectStateAsync(dbPath);

        if (state == DbState.AlreadyMigrated)
            return false;

        if (state == DbState.Empty)
        {
            DeleteFiles(dbPath);
            return false;
        }

        logger.LogInformation("Legacy SQLite database detected — beginning data-preserving transition");

        var backupPath = dbPath + ".migration_backup";

        if (File.Exists(backupPath))
        {
            // Previous transition crashed after rename — the backup IS the original data.
            logger.LogWarning("Previous migration backup found at {Path} — retrying transition", backupPath);
            DeleteFiles(dbPath);
        }
        else
        {
            SqliteConnection.ClearAllPools();
            await Task.Delay(200); // Windows file handle release lag

            File.Move(dbPath, backupPath);
            MoveIfExists(dbPath + "-wal", backupPath + "-wal");
            MoveIfExists(dbPath + "-shm", backupPath + "-shm");
        }

        // Create fresh database with full schema via migrations
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
        }

        // Copy data from backup into fresh database
        try
        {
            await CopyDataAsync(dbPath, backupPath, logger);
            logger.LogInformation("Data transition completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Data copy FAILED. Backup preserved at {Path}. " +
                "New database has correct schema but may be missing data. " +
                "You can manually recover from the backup.",
                backupPath);
            return true;
        }

        try
        {
            DeleteFiles(backupPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete migration backup at {Path}", backupPath);
        }

        return true;
    }

    private enum DbState { AlreadyMigrated, NeedsTransition, Empty }

    private static async Task<DbState> DetectStateAsync(string dbPath)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        // Check for migration history table
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
        if (await cmd.ExecuteScalarAsync() is not null)
        {
            // Verify the current migrations are applied (not just any old migration names)
            cmd.CommandText =
                "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" " +
                "WHERE MigrationId LIKE '%_InitialSqlServer' OR MigrationId LIKE '%_AddSubscriptionFields' " +
                "OR MigrationId LIKE '%_AddFundamentalsCache'";
            var currentMigrationCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            cmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN " +
                "('ScanRuns','AspNetUsers','AspNetRoles','FundamentalsCache')";
            var keyTableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // Fully migrated: has the CURRENT migration names and all key tables exist
            if (currentMigrationCount >= 3 && keyTableCount >= 4)
                return DbState.AlreadyMigrated;

            // Stale or broken state: has old migration names or missing tables
            return DbState.NeedsTransition;
        }

        // No history table — check if any app tables exist
        cmd.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        var tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        return tableCount == 0 ? DbState.Empty : DbState.NeedsTransition;
    }

    private static async Task CopyDataAsync(string newDbPath, string backupPath, ILogger logger)
    {
        await using var conn = new SqliteConnection($"Data Source={newDbPath}");
        await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = OFF";
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "ATTACH DATABASE @path AS backup_db";
            cmd.Parameters.AddWithValue("@path", backupPath);
            await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            // Copy in FK-safe order: parents before children
            // Independent tables
            await CopyTableAsync(conn, "ScanRuns", logger);
            await CopyTableAsync(conn, "ScanCheckpoints", logger);
            await CopyTableAsync(conn, "ScanCommands", logger);
            await CopyTableAsync(conn, "DailyBriefs", logger);
            await CopyTableAsync(conn, "EconomicSeries", logger);
            await CopyTableAsync(conn, "EconomicSyncTrackers", logger);
            await CopyTableAsync(conn, "FundamentalsCache", logger);

            // Identity parents
            await CopyTableAsync(conn, "AspNetRoles", logger);
            await CopyTableAsync(conn, "AspNetUsers", logger);

            // Child tables
            await CopyTableAsync(conn, "ScanCandidates", logger);
            await CopyTableAsync(conn, "EconomicObservations", logger);

            // Identity children
            await CopyTableAsync(conn, "AspNetRoleClaims", logger);
            await CopyTableAsync(conn, "AspNetUserClaims", logger);
            await CopyTableAsync(conn, "AspNetUserLogins", logger);
            await CopyTableAsync(conn, "AspNetUserRoles", logger);
            await CopyTableAsync(conn, "AspNetUserTokens", logger);
        }
        finally
        {
            await using var detach = conn.CreateCommand();
            detach.CommandText = "DETACH DATABASE backup_db";
            await detach.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task CopyTableAsync(SqliteConnection conn, string table, ILogger logger)
    {
        await using var cmd = conn.CreateCommand();

        // Check if table exists in backup
        cmd.CommandText = "SELECT COUNT(*) FROM backup_db.sqlite_master WHERE type='table' AND name=@name";
        cmd.Parameters.AddWithValue("@name", table);
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0)
            return;

        // Get columns from destination (new schema)
        cmd.Parameters.Clear();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        var destColumns = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                destColumns.Add(reader.GetString(1));
        }

        // Get columns from backup (old schema)
        cmd.CommandText = $"PRAGMA backup_db.table_info(\"{table}\")";
        var srcColumns = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                srcColumns.Add(reader.GetString(1));
        }

        // Only copy columns that exist in both schemas
        var common = destColumns.Intersect(srcColumns, StringComparer.OrdinalIgnoreCase).ToList();
        if (common.Count == 0)
            return;

        // Check row count
        cmd.CommandText = $"SELECT COUNT(*) FROM backup_db.\"{table}\"";
        var rowCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        if (rowCount == 0)
            return;

        var cols = string.Join(", ", common.Select(c => $"\"{c}\""));
        cmd.CommandText = $"INSERT INTO main.\"{table}\" ({cols}) SELECT {cols} FROM backup_db.\"{table}\"";
        var inserted = await cmd.ExecuteNonQueryAsync();

        logger.LogInformation("Copied {Count} rows into {Table}", inserted, table);
    }

    private static void DeleteFiles(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
        if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
    }

    private static void MoveIfExists(string src, string dest)
    {
        if (File.Exists(src)) File.Move(src, dest);
    }
}
