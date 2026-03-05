using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Infrastructure.AlphaVantage;
using Signavex.Infrastructure.Anthropic;
using Signavex.Infrastructure.Caching;
using Signavex.Infrastructure.Fred;
using Signavex.Infrastructure.Persistence;
using Signavex.Infrastructure.Polygon;

namespace Signavex.Infrastructure;

/// <summary>
/// Registers all Signavex infrastructure (data provider) implementations with the DI container.
/// Provider implementations use typed HttpClients for proper lifecycle management.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSignavexInfrastructure(
        this IServiceCollection services,
        DataProviderOptions providerOptions,
        string dataDirectory = "data",
        string databaseProvider = "Sqlite",
        string connectionString = "")
    {
        services.AddMemoryCache();

        // Database persistence — SQLite (default/local) or SQL Server (production)
        if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContextFactory<SignavexDbContext>(options =>
                options.UseSqlServer(connectionString));
        }
        else
        {
            var dbPath = Path.Combine(dataDirectory, "signavex.db");
            Directory.CreateDirectory(dataDirectory);
            services.AddDbContextFactory<SignavexDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}")
                    .ReplaceService<IMigrationsSqlGenerator, SqlServerTypeSqliteGenerator>());
        }

        services.AddSingleton<IScanStateStore, SqliteScanStateStore>();
        services.AddSingleton<IScanHistoryStore, SqliteScanHistoryStore>();
        services.AddSingleton<IScanCommandStore, SqliteScanCommandStore>();
        services.AddSingleton<IEconomicDataStore, SqliteEconomicDataStore>();
        services.AddSingleton<IDailyBriefStore, SqliteDailyBriefStore>();

        // Shared rate limiter for all Polygon/Massive API calls.
        // Free tier = 5 req/min. Paid tiers can override via config.
        var polygonRpm = providerOptions.Polygon.MaxRequestsPerMinute > 0
            ? providerOptions.Polygon.MaxRequestsPerMinute
            : 5;

        var polygonLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = polygonRpm,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = polygonRpm,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 500,
            AutoReplenishment = true,
        });

        services.AddSingleton<RateLimiter>(polygonLimiter);

        services.AddTransient(sp =>
            new PolygonRateLimitingHandler(
                sp.GetRequiredService<RateLimiter>(),
                sp.GetRequiredService<ILogger<PolygonRateLimitingHandler>>()));

        services.AddHttpClient<IMarketDataProvider, PolygonMarketDataProvider>(client =>
        {
            client.BaseAddress = new Uri(providerOptions.Polygon.BaseUrl);
        }).AddHttpMessageHandler<PolygonRateLimitingHandler>();

        services.AddHttpClient<INewsDataProvider, PolygonNewsProvider>(client =>
        {
            client.BaseAddress = new Uri(providerOptions.Polygon.BaseUrl);
        }).AddHttpMessageHandler<PolygonRateLimitingHandler>();

        // Register AlphaVantage as the inner provider, then wrap with caching decorator
        services.AddHttpClient<AlphaVantageFundamentalsProvider>(client =>
        {
            client.BaseAddress = new Uri(providerOptions.AlphaVantage.BaseUrl);
        });
        services.AddScoped<IFundamentalsProvider>(sp =>
            new CachedFundamentalsProvider(
                sp.GetRequiredService<AlphaVantageFundamentalsProvider>(),
                sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));

        services.AddHttpClient<IEconomicDataProvider, FredEconomicDataProvider>(client =>
        {
            client.BaseAddress = new Uri(providerOptions.Fred.BaseUrl);
        });

        services.AddHttpClient<IFredApiClient, FredApiClient>(client =>
        {
            client.BaseAddress = new Uri(providerOptions.Fred.BaseUrl);
        });

        services.AddHttpClient<IAiBriefGenerator, AnthropicBriefGenerator>(client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com");
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        return services;
    }
}
