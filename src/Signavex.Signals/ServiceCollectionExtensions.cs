using Microsoft.Extensions.DependencyInjection;
using Signavex.Domain.Interfaces;
using Signavex.Signals.Fundamental;
using Signavex.Signals.Market;
using Signavex.Signals.Sentiment;
using Signavex.Signals.Technical;

namespace Signavex.Signals;

/// <summary>
/// Registers all signal implementations with the DI container.
/// Adding a new signal: implement IStockSignal or IMarketSignal and add it here.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSignavexSignals(this IServiceCollection services)
    {
        // Tier 2: Stock-level signals
        services.AddTransient<IStockSignal, VolumeThresholdSignal>();
        services.AddTransient<IStockSignal, MovingAverageCrossoverSignal>();
        services.AddTransient<IStockSignal, SupportResistanceSignal>();
        services.AddTransient<IStockSignal, TrendDirectionSignal>();
        services.AddTransient<IStockSignal, ChannelPositionSignal>();
        services.AddTransient<IStockSignal, RsiSignal>();
        services.AddTransient<IStockSignal, MacdSignal>();
        services.AddTransient<IStockSignal, BollingerBandSignal>();
        services.AddTransient<IStockSignal, NewsSentimentSignal>();
        services.AddTransient<IStockSignal, AnalystRatingSignal>();
        services.AddTransient<IStockSignal, PeRatioSignal>();
        services.AddTransient<IStockSignal, DebtEquitySignal>();
        services.AddTransient<IStockSignal, EarningsTrendSignal>();

        // Tier 1: Market-level signals
        services.AddTransient<IMarketSignal, MarketTrendSignal>();
        services.AddTransient<IMarketSignal, InterestRateSignal>();
        services.AddTransient<IMarketSignal, VixLevelSignal>();
        services.AddTransient<IMarketSignal, SectorMomentumSignal>();
        services.AddTransient<IMarketSignal, EconomicHealthSignal>();

        return services;
    }
}
