using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Signavex.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI tooling (dotnet ef migrations add).
/// Targets SQL Server for migration generation. SQLite development uses EnsureCreated().
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SignavexDbContext>
{
    public SignavexDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SignavexDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Signavex_Design;Trusted_Connection=True;");

        return new SignavexDbContext(optionsBuilder.Options);
    }
}
