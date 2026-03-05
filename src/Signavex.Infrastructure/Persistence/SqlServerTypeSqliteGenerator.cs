using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;

namespace Signavex.Infrastructure.Persistence;

/// <summary>
/// Custom SQLite migration SQL generator that translates SQL Server column types
/// to SQLite equivalents. This allows migrations scaffolded against SQL Server
/// (via DesignTimeDbContextFactory) to run on SQLite without modification.
/// </summary>
public class SqlServerTypeSqliteGenerator : SqliteMigrationsSqlGenerator
{
    public SqlServerTypeSqliteGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        IRelationalAnnotationProvider migrationsAnnotations)
        : base(dependencies, migrationsAnnotations) { }

    protected override void ColumnDefinition(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel model,
        MigrationCommandListBuilder builder)
    {
        operation.ColumnType = TranslateType(operation.ColumnType);
        base.ColumnDefinition(schema, table, name, operation, model, builder);
    }

    protected override string GetColumnType(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel model)
    {
        var type = base.GetColumnType(schema, table, name, operation, model);
        return TranslateType(type) ?? type ?? "TEXT";
    }

    private static string? TranslateType(string? sqlType)
    {
        if (sqlType is null) return null;

        var upper = sqlType.ToUpperInvariant();

        // String types → TEXT
        if (upper.Contains("VARCHAR") || upper.Contains("NCHAR") || upper.Contains("CHAR")
            || upper == "NTEXT" || upper == "XML")
            return "TEXT";

        // Boolean → INTEGER
        if (upper == "BIT")
            return "INTEGER";

        // Date/time → TEXT (SQLite stores as ISO-8601 strings)
        if (upper.StartsWith("DATETIME") || upper == "DATE" || upper == "DATETIMEOFFSET"
            || upper == "SMALLDATETIME" || upper == "TIME")
            return "TEXT";

        // Floating point → REAL
        if (upper == "FLOAT" || upper == "REAL" || upper.StartsWith("DECIMAL")
            || upper.StartsWith("NUMERIC") || upper == "MONEY" || upper == "SMALLMONEY")
            return "REAL";

        // Integer types → INTEGER
        if (upper == "INT" || upper == "BIGINT" || upper == "SMALLINT" || upper == "TINYINT")
            return "INTEGER";

        // Binary → BLOB
        if (upper.Contains("VARBINARY") || upper.Contains("BINARY") || upper == "IMAGE")
            return "BLOB";

        // GUID → TEXT
        if (upper == "UNIQUEIDENTIFIER")
            return "TEXT";

        return sqlType;
    }
}
