using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Polygon.Dtos;

namespace Signavex.Infrastructure.Polygon;

public class PolygonNewsProvider : INewsDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly DataProviderOptions _options;
    private readonly ILogger<PolygonNewsProvider> _logger;

    public PolygonNewsProvider(
        HttpClient httpClient,
        IOptions<DataProviderOptions> options,
        ILogger<PolygonNewsProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<NewsItem>> GetRecentNewsAsync(string ticker, int days)
    {
        try
        {
            var since = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-dd");

            var url = $"/v2/reference/news?ticker={ticker}&limit=50" +
                      $"&published_utc.gte={since}&apiKey={_options.Polygon.ApiKey}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<PolygonNewsResponse>(json);

            if (data?.Results is null or { Count: 0 })
                return Enumerable.Empty<NewsItem>();

            return data.Results
                .Where(r => r.Title is not null)
                .Select(r => new NewsItem(
                    ticker,
                    r.Title!,
                    r.Description,
                    r.Publisher?.Name,
                    DateTime.TryParse(r.PublishedUtc, out var published) ? published.ToUniversalTime() : DateTime.UtcNow,
                    ExtractSentimentForTicker(r.Insights, ticker)
                ));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch news for {Ticker}", ticker);
            return Enumerable.Empty<NewsItem>();
        }
    }

    internal static double? ExtractSentimentForTicker(List<PolygonNewsInsight>? insights, string ticker)
    {
        var match = insights?.FirstOrDefault(i =>
            string.Equals(i.Ticker, ticker, StringComparison.OrdinalIgnoreCase));

        return match?.Sentiment?.ToLowerInvariant() switch
        {
            "positive" => 0.7,
            "negative" => -0.7,
            "neutral" => 0.0,
            _ => null
        };
    }
}
