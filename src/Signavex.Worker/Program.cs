using Microsoft.EntityFrameworkCore;
using Signavex.Domain.Configuration;
using Signavex.Engine;
using Signavex.Infrastructure;
using Signavex.Infrastructure.Persistence;
using Signavex.Signals;
using Signavex.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Signavex Scanner";
});

// Bind configuration options
builder.Services.Configure<SignavexOptions>(
    builder.Configuration.GetSection(SignavexOptions.SectionName));

builder.Services.Configure<DataProviderOptions>(
    builder.Configuration.GetSection(DataProviderOptions.SectionName));

builder.Services.Configure<AnthropicOptions>(
    builder.Configuration.GetSection(AnthropicOptions.SectionName));

var providerOptions = builder.Configuration
    .GetSection(DataProviderOptions.SectionName)
    .Get<DataProviderOptions>() ?? new DataProviderOptions();

var signavexOptions = builder.Configuration
    .GetSection(SignavexOptions.SectionName)
    .Get<SignavexOptions>() ?? new SignavexOptions();

// Register domain layers (same chain as Web minus Blazor)
builder.Services
    .AddSignavexSignals()
    .AddSignavexEngine()
    .AddSignavexInfrastructure(providerOptions, signavexOptions.ConnectionString);

// Worker services
builder.Services.AddSingleton<WorkerScanOrchestrator>();
builder.Services.AddHostedService<ScanCommandPollingService>();
builder.Services.AddHostedService<ScanResumeBackgroundService>();
builder.Services.AddHostedService<DailyScanBackgroundService>();
builder.Services.AddHostedService<EconomicDataSyncService>();
builder.Services.AddHostedService<DailyBriefBackgroundService>();
builder.Services.AddHostedService<FundamentalsBackfillService>();

var host = builder.Build();

// Initialize database — migrations handle all schema and seed data
using (var scope = host.Services.CreateScope())
{
    await using var db = await scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<SignavexDbContext>>()
        .CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

host.Run();
