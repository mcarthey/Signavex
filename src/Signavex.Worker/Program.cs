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

var dataDirectory = !string.IsNullOrWhiteSpace(signavexOptions.DataDirectory)
    ? signavexOptions.DataDirectory
    : Path.Combine(builder.Environment.ContentRootPath, "data");

// Register domain layers (same chain as Web minus Blazor)
builder.Services
    .AddSignavexSignals()
    .AddSignavexEngine()
    .AddSignavexInfrastructure(providerOptions, dataDirectory,
        signavexOptions.DatabaseProvider, signavexOptions.ConnectionString);

// Worker services
builder.Services.AddSingleton<WorkerScanOrchestrator>();
builder.Services.AddHostedService<ScanCommandPollingService>();
builder.Services.AddHostedService<ScanResumeBackgroundService>();
builder.Services.AddHostedService<DailyScanBackgroundService>();
builder.Services.AddHostedService<EconomicDataSyncService>();
builder.Services.AddHostedService<DailyBriefBackgroundService>();

var host = builder.Build();

// Initialize database and seed economic data
using (var scope = host.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SignavexDbContext>>();
    using var db = await factory.CreateDbContextAsync();

    if (string.Equals(signavexOptions.DatabaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL");
        await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000");
    }

    var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("EconomicDataSeeder");
    await EconomicDataSeeder.SeedAsync(factory, seedLogger);
}

host.Run();
