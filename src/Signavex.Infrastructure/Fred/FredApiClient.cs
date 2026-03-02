using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models.Economic;
using Signavex.Infrastructure.Fred.Dtos;

namespace Signavex.Infrastructure.Fred;

public class FredApiClient : IFredApiClient
{
    private readonly HttpClient _httpClient;
    private readonly DataProviderOptions _options;
    private readonly ILogger<FredApiClient> _logger;

    public FredApiClient(
        HttpClient httpClient,
        IOptions<DataProviderOptions> options,
        ILogger<FredApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EconomicObservation>> GetObservationsAsync(
        string seriesId, DateOnly? startDate = null, CancellationToken ct = default)
    {
        try
        {
            var url = $"/fred/series/observations?series_id={seriesId}" +
                      $"&api_key={_options.Fred.ApiKey}&file_type=json&sort_order=asc";

            if (startDate.HasValue)
                url += $"&observation_start={startDate.Value:yyyy-MM-dd}";

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<FredSeriesResponse>(json);

            if (data?.Observations is null)
                return Array.Empty<EconomicObservation>();

            var results = new List<EconomicObservation>();
            foreach (var obs in data.Observations)
            {
                if (string.IsNullOrWhiteSpace(obs.Value) || obs.Value == "." || obs.Date is null)
                    continue;

                if (!double.TryParse(obs.Value, CultureInfo.InvariantCulture, out var value))
                    continue;

                if (!DateOnly.TryParse(obs.Date, CultureInfo.InvariantCulture, out var date))
                    continue;

                results.Add(new EconomicObservation(seriesId, date, value));
            }

            _logger.LogDebug("Fetched {Count} observations for {SeriesId}", results.Count, seriesId);
            return results.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch observations for {SeriesId}", seriesId);
            return Array.Empty<EconomicObservation>();
        }
    }
}
