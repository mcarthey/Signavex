using Microsoft.Extensions.Options;
using Signavex.Domain.Configuration;

namespace Signavex.Infrastructure.Tests.Helpers;

internal static class TestOptionsFactory
{
    public static IOptions<DataProviderOptions> CreateDefault()
    {
        var options = new DataProviderOptions
        {
            Polygon = new ApiProviderOptions { ApiKey = "test-polygon-key", BaseUrl = "https://api.polygon.io" },
            AlphaVantage = new ApiProviderOptions { ApiKey = "test-av-key", BaseUrl = "https://www.alphavantage.co" },
            Fred = new ApiProviderOptions { ApiKey = "test-fred-key", BaseUrl = "https://api.stlouisfed.org" }
        };
        return Options.Create(options);
    }
}
