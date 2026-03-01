using Microsoft.Extensions.Options;
using Signavex.Domain.Configuration;

namespace Signavex.Web.Services;

/// <summary>
/// Checks whether required data provider API keys are configured.
/// Used by Dashboard/Backtest pages to show warnings before scanning.
/// </summary>
public class ApiKeyValidationService
{
    private readonly DataProviderOptions _options;

    public ApiKeyValidationService(IOptions<DataProviderOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<string> GetMissingKeys()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.Polygon.ApiKey))
            missing.Add("Polygon (OHLCV + News)");

        if (string.IsNullOrWhiteSpace(_options.AlphaVantage.ApiKey))
            missing.Add("Alpha Vantage (Fundamentals)");

        if (string.IsNullOrWhiteSpace(_options.Fred.ApiKey))
            missing.Add("FRED (Economic Data)");

        return missing;
    }

    public bool HasAllKeys => GetMissingKeys().Count == 0;
}
