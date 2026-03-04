using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Signavex.Infrastructure.Persistence;

namespace Signavex.Infrastructure;

public static class RoleSeeder
{
    public static readonly string[] Roles = ["Free", "Pro"];

    public static async Task SeedAsync(IServiceProvider services, ILogger? logger = null)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger?.LogInformation("Created role: {Role}", role);
            }
        }
    }
}
