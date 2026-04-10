using Microsoft.EntityFrameworkCore;
using Signavex.Infrastructure.Persistence;
using Signavex.Infrastructure.Persistence.Entities;

// ---- Configuration ----
// Usage:
//   MigrateData <target-connection-string> <source-connection-string-or-sqlite-path>
//
// Source detection: if the source arg ends in .db it's treated as SQLite (read-only),
// otherwise it's treated as a SQL Server connection string.
//
// Defaults (no args): source = local SQLite backup, target = LocalDB

var targetConn = args.Length > 0
    ? args[0]
    : @"Server=(localdb)\MSSQLLocalDB;Database=Signavex;Trusted_Connection=True;TrustServerCertificate=True;";

var sourceArg = args.Length > 1
    ? args[1]
    : @"E:\Documents\Work\dev\repos\Signavex\tools\signavex-sqlite-backup.db";

var sourceIsSqlite = sourceArg.EndsWith(".db", StringComparison.OrdinalIgnoreCase);

Console.WriteLine($"Source: {(sourceIsSqlite ? "SQLite at " + sourceArg : "SQL Server at " + Truncate(sourceArg, 50))}");
Console.WriteLine($"Target: SQL Server at {Truncate(targetConn, 50)}");
Console.WriteLine();

// ---- Build contexts ----
var sourceOptions = new DbContextOptionsBuilder<SignavexDbContext>();
if (sourceIsSqlite)
    sourceOptions.UseSqlite($"Data Source={sourceArg};Mode=ReadOnly");
else
    sourceOptions.UseSqlServer(sourceArg);

var targetOptions = new DbContextOptionsBuilder<SignavexDbContext>()
    .UseSqlServer(targetConn)
    .Options;

using var source = new SignavexDbContext(sourceOptions.Options);
using var target = new SignavexDbContext(targetOptions);

static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

// ---- Migrate tables in dependency order ----

await MigrateTable("AspNetRoles",
    source.Roles.AsNoTracking(),
    target.Roles);

await MigrateTable("AspNetUsers",
    source.Users.AsNoTracking(),
    target.Users);

await MigrateTable("AspNetUserRoles",
    source.UserRoles.AsNoTracking(),
    target.UserRoles);

await MigrateTable("EconomicSeries",
    source.EconomicSeries.AsNoTracking(),
    target.EconomicSeries);

await MigrateTable("EconomicObservations",
    source.EconomicObservations.AsNoTracking(),
    target.EconomicObservations);

await MigrateTable("EconomicSyncTrackers",
    source.EconomicSyncTrackers.AsNoTracking(),
    target.EconomicSyncTrackers);

// ScanRuns first WITHOUT candidates (to avoid dual identity insert conflict)
await MigrateTable("ScanRuns",
    source.ScanRuns.AsNoTracking(),
    target.ScanRuns);

await MigrateTable("ScanCandidates",
    source.ScanCandidates.AsNoTracking(),
    target.ScanCandidates);

await MigrateTable("ScanCheckpoints",
    source.ScanCheckpoints.AsNoTracking(),
    target.ScanCheckpoints);

await MigrateTable("ScanCommands",
    source.ScanCommands.AsNoTracking(),
    target.ScanCommands);

await MigrateTable("DailyBriefs",
    source.DailyBriefs.AsNoTracking(),
    target.DailyBriefs);

await MigrateTable("FundamentalsCache",
    source.FundamentalsCache.AsNoTracking(),
    target.FundamentalsCache);

Console.WriteLine();
Console.WriteLine("Migration complete!");

// ---- Helper ----
async Task MigrateTable<T>(string name, IQueryable<T> sourceQuery, DbSet<T> targetSet) where T : class
{
    var existing = await targetSet.CountAsync();
    if (existing > 0)
    {
        Console.WriteLine($"  SKIP {name} — already has {existing} rows");
        return;
    }

    var rows = await sourceQuery.ToListAsync();
    if (rows.Count == 0)
    {
        Console.WriteLine($"  SKIP {name} — empty in source");
        return;
    }

    // SQL Server needs IDENTITY_INSERT ON for tables with auto-generated IDs.
    // Check if the entity has a single auto-generated key (identity column).
    var tableName = targetSet.EntityType.GetTableName();
    var key = targetSet.EntityType.FindPrimaryKey();
    var hasIdentity = key?.Properties.Count == 1
        && key.Properties[0].ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd;

    if (hasIdentity)
    {
        await using var transaction = await target.Database.BeginTransactionAsync();
        await target.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [" + tableName + "] ON");
        targetSet.AddRange(rows);
        await target.SaveChangesAsync();
        await target.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [" + tableName + "] OFF");
        await transaction.CommitAsync();
    }
    else
    {
        targetSet.AddRange(rows);
        await target.SaveChangesAsync();
    }

    // Detach all tracked entities so subsequent tables don't conflict
    target.ChangeTracker.Clear();

    Console.WriteLine($"  OK   {name} — {rows.Count} rows migrated");
}
