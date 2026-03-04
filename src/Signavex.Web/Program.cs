using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Signavex.Domain.Configuration;
using Signavex.Engine;
using Signavex.Infrastructure;
using Signavex.Infrastructure.Persistence;
using Signavex.Signals;
using Signavex.Web.Components;
using Signavex.Web.Services;
using Signavex.Worker;

var builder = WebApplication.CreateBuilder(args);

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

// Register domain layers
builder.Services
    .AddSignavexSignals()
    .AddSignavexEngine()
    .AddSignavexInfrastructure(providerOptions, dataDirectory,
        signavexOptions.DatabaseProvider, signavexOptions.ConnectionString);

// ASP.NET Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<SignavexDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/login";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

// Application services
builder.Services.AddSingleton<ScanDashboardService>();
builder.Services.AddSingleton<BacktestRunnerService>();
builder.Services.AddSingleton<ApiKeyValidationService>();
builder.Services.AddSingleton<EconomicDashboardService>();
builder.Services.AddSingleton<DailyBriefService>();

// Optionally run Worker background services in-process (production single-process mode)
if (signavexOptions.RunBackgroundServices)
{
    builder.Services.AddSingleton<WorkerScanOrchestrator>();
    builder.Services.AddHostedService<ScanCommandPollingService>();
    builder.Services.AddHostedService<ScanResumeBackgroundService>();
    builder.Services.AddHostedService<DailyScanBackgroundService>();
    builder.Services.AddHostedService<EconomicDataSyncService>();
    builder.Services.AddHostedService<DailyBriefBackgroundService>();
}

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Initialize database and seed economic data
using (var scope = app.Services.CreateScope())
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/account/login");
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
