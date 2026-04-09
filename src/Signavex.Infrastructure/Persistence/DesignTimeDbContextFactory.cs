using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Signavex.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI tooling (dotnet ef migrations add, dotnet ef database update).
/// Uses LocalDB with the same database name as the application.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SignavexDbContext>
{
    public SignavexDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SignavexDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Signavex;Trusted_Connection=True;TrustServerCertificate=True;");

        return new SignavexDbContext(optionsBuilder.Options);
    }
}
