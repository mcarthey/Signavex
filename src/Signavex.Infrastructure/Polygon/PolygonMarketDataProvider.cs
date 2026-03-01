using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Signavex.Domain.Configuration;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Polygon.Dtos;

namespace Signavex.Infrastructure.Polygon;

public class PolygonMarketDataProvider : IMarketDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly DataProviderOptions _options;
    private readonly ILogger<PolygonMarketDataProvider> _logger;

    public PolygonMarketDataProvider(
        HttpClient httpClient,
        IOptions<DataProviderOptions> options,
        ILogger<PolygonMarketDataProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<OhlcvRecord>> GetDailyOhlcvAsync(string ticker, int days)
    {
        try
        {
            var to = DateTime.UtcNow;
            var from = to.AddDays(-(days * 1.5)); // Request extra calendar days to account for weekends/holidays
            var fromStr = from.ToString("yyyy-MM-dd");
            var toStr = to.ToString("yyyy-MM-dd");

            var url = $"/v2/aggs/ticker/{ticker}/range/1/day/{fromStr}/{toStr}" +
                      $"?apiKey={_options.Polygon.ApiKey}&limit={days}&sort=asc&adjusted=true";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<PolygonAggregatesResponse>(json);

            if (data?.Results is null or { Count: 0 })
                return Enumerable.Empty<OhlcvRecord>();

            return data.Results.Select(r => new OhlcvRecord(
                ticker,
                DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(r.Timestamp).UtcDateTime),
                r.Open,
                r.High,
                r.Low,
                r.Close,
                r.Volume
            ));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch OHLCV data for {Ticker}", ticker);
            return Enumerable.Empty<OhlcvRecord>();
        }
    }

    public Task<IEnumerable<string>> GetIndexConstituentsAsync(MarketIndex index)
    {
        try
        {
            var fileName = index switch
            {
                MarketIndex.SP500 => "sp500.json",
                MarketIndex.SP400 => "sp400.json",
                _ => throw new ArgumentOutOfRangeException(nameof(index), $"No constituent data for {index}")
            };

            var resourceName = $"Signavex.Infrastructure.IndexData.{fileName}";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

            if (stream is null)
            {
                _logger.LogWarning("Embedded resource {Resource} not found", resourceName);
                return Task.FromResult(Enumerable.Empty<string>());
            }

            var tickers = JsonSerializer.Deserialize<string[]>(stream);
            return Task.FromResult<IEnumerable<string>>(tickers ?? Array.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load index constituents for {Index}", index);
            return Task.FromResult(Enumerable.Empty<string>());
        }
    }
}
