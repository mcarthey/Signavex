using Microsoft.Extensions.DependencyInjection;
using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Infrastructure.AlphaVantage;
using Signavex.Infrastructure.Fred;
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
        DataProviderOptions providerOptions)
    {
        services.AddHttpClient<IMarketDataProvider, PolygonMarketDataProvider>(client =>
        {
            client.BaseAddress = new Uri(providerOptions.Polygon.BaseUrl);
        });

        services.AddHttpClient<INewsDataProvider, PolygonNewsProvider>(client =>
        {
            client.BaseAddress = new Uri(providerOptions.Polygon.BaseUrl);
        });

        services.AddHttpClient<IFundamentalsProvider, AlphaVantageFundamentalsProvider>(client =>
        {
            client.BaseAddress = new Uri(providerOptions.AlphaVantage.BaseUrl);
        });

        services.AddHttpClient<IEconomicDataProvider, FredEconomicDataProvider>(client =>
        {
            client.BaseAddress = new Uri(providerOptions.Fred.BaseUrl);
        });

        return services;
    }
}
