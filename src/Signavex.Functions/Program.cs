using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Signavex.Domain.Configuration;
using Signavex.Engine;
using Signavex.Infrastructure;
using Signavex.Infrastructure.Persistence;
using Signavex.Signals;
using Signavex.Functions.Orchestrators;
using Signavex.Functions.Security;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();

        // Bind configuration options — same pattern as Web and Worker
        services.Configure<SignavexOptions>(
            context.Configuration.GetSection(SignavexOptions.SectionName));

        services.Configure<DataProviderOptions>(
            context.Configuration.GetSection(DataProviderOptions.SectionName));

        services.Configure<AnthropicOptions>(
            context.Configuration.GetSection(AnthropicOptions.SectionName));

        var providerOptions = context.Configuration
            .GetSection(DataProviderOptions.SectionName)
            .Get<DataProviderOptions>() ?? new DataProviderOptions();

        var signavexOptions = context.Configuration
            .GetSection(SignavexOptions.SectionName)
            .Get<SignavexOptions>() ?? new SignavexOptions();

        // Register domain layers (same chain as Web/Worker)
        services
            .AddSignavexSignals()
            .AddSignavexEngine()
            .AddSignavexInfrastructure(providerOptions, signavexOptions.ConnectionString);

        // Orchestrators — self-contained work units, invoked by Function triggers
        services.AddSingleton<ScanOrchestrator>();
        services.AddSingleton<BriefOrchestrator>();
        services.AddSingleton<EconomicSyncOrchestrator>();
        services.AddSingleton<FundamentalsBackfillOrchestrator>();

        // Admin authorization — checks x-signavex-admin-key header on HTTP-triggered admin ops
        services.AddSingleton<AdminKeyAuthorizer>();
    })
    .Build();

// Initialize database — migrations handle all schema and seed data.
// Safe to run from multiple processes; EF's MigrateAsync is idempotent.
using (var scope = host.Services.CreateScope())
{
    await using var db = await scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<SignavexDbContext>>()
        .CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

await host.RunAsync();
