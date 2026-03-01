using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Infrastructure.Polygon;

/// <summary>
/// Polygon.io implementation of INewsDataProvider.
/// Full implementation in Phase 2.
/// </summary>
public class PolygonNewsProvider : INewsDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly DataProviderOptions _options;

    public PolygonNewsProvider(HttpClient httpClient, IOptions<DataProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<IEnumerable<NewsItem>> GetRecentNewsAsync(string ticker, int days)
        => throw new NotImplementedException("Polygon news provider — implemented in Phase 2");
}
