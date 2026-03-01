using Signavex.Domain.Configuration;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Infrastructure.Polygon;

/// <summary>
/// Polygon.io implementation of IMarketDataProvider.
/// Full implementation in Phase 2.
/// </summary>
public class PolygonMarketDataProvider : IMarketDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly DataProviderOptions _options;

    public PolygonMarketDataProvider(HttpClient httpClient, IOptions<DataProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<IEnumerable<OhlcvRecord>> GetDailyOhlcvAsync(string ticker, int days)
        => throw new NotImplementedException("Polygon OHLCV provider — implemented in Phase 2");

    public Task<IEnumerable<string>> GetIndexConstituentsAsync(MarketIndex index)
        => throw new NotImplementedException("Index constituent provider — implemented in Phase 2");
}
