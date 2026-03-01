using Microsoft.Extensions.DependencyInjection;

namespace Signavex.Engine;

/// <summary>
/// Registers all Signavex engine services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSignavexEngine(this IServiceCollection services)
    {
        services.AddSingleton<ScoreCalculator>();
        services.AddScoped<MarketEvaluator>();
        services.AddScoped<StockEvaluator>();
        services.AddScoped<UniverseProvider>();
        services.AddScoped<ScanEngine>();
        services.AddScoped<BacktestService>();
        services.AddHostedService<DailyScanBackgroundService>();

        return services;
    }
}
