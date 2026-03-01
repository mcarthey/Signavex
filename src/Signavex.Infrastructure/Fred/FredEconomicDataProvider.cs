using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Infrastructure.Fred.Dtos;

namespace Signavex.Infrastructure.Fred;

public class FredEconomicDataProvider : IEconomicDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly DataProviderOptions _options;
    private readonly ILogger<FredEconomicDataProvider> _logger;

    public FredEconomicDataProvider(
        HttpClient httpClient,
        IOptions<DataProviderOptions> options,
        ILogger<FredEconomicDataProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MacroIndicators> GetMacroIndicatorsAsync()
    {
        try
        {
            var fedFundsTask = FetchSeriesAsync("FEDFUNDS", limit: 2);
            var vixTask = FetchSeriesAsync("VIXCLS", limit: 1);

            await Task.WhenAll(fedFundsTask, vixTask);

            var fedFundsObs = fedFundsTask.Result;
            var vixObs = vixTask.Result;

            var fedCurrent = fedFundsObs.Count > 0 ? ParseFredValue(fedFundsObs[0].Value) : null;
            var fedPrevious = fedFundsObs.Count > 1 ? ParseFredValue(fedFundsObs[1].Value) : null;
            var vix = vixObs.Count > 0 ? ParseFredValue(vixObs[0].Value) : null;

            return new MacroIndicators(fedCurrent, fedPrevious, vix, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch macro indicators from FRED");
            return new MacroIndicators(null, null, null, DateTime.UtcNow);
        }
    }

    private async Task<List<FredObservation>> FetchSeriesAsync(string seriesId, int limit)
    {
        var url = $"/fred/series/observations?series_id={seriesId}" +
                  $"&api_key={_options.Fred.ApiKey}&file_type=json&sort_order=desc&limit={limit}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<FredSeriesResponse>(json);

        return data?.Observations ?? new List<FredObservation>();
    }

    private static double? ParseFredValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == ".")
            return null;

        return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }
}
