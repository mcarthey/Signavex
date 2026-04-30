using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Infrastructure.AlphaVantage.Dtos;

namespace Signavex.Infrastructure.AlphaVantage;

public class AlphaVantageFundamentalsProvider : IFundamentalsProvider
{
    private readonly HttpClient _httpClient;
    private readonly DataProviderOptions _options;
    private readonly ILogger<AlphaVantageFundamentalsProvider> _logger;

    public AlphaVantageFundamentalsProvider(
        HttpClient httpClient,
        IOptions<DataProviderOptions> options,
        ILogger<AlphaVantageFundamentalsProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FundamentalsData> GetFundamentalsAsync(string ticker)
    {
        try
        {
            var overview = await FetchOverviewAsync(ticker);
            var earnings = await FetchEarningsAsync(ticker);
            var balanceSheet = await FetchBalanceSheetAsync(ticker);

            var latestQuarter = earnings?.QuarterlyEarnings?.FirstOrDefault();

            return new FundamentalsData(
                Ticker: ticker,
                PeRatio: ParseDouble(overview?.PERatio),
                IndustryPeRatio: SectorPeAverages.Lookup(overview?.Sector),
                DebtToEquityRatio: ComputeDebtToEquity(balanceSheet),
                EpsCurrentQuarter: ParseDouble(latestQuarter?.ReportedEPS),
                EpsEstimateCurrentQuarter: ParseDouble(latestQuarter?.EstimatedEPS),
                EpsPreviousYear: ParseDouble(overview?.EPS),
                AnalystRating: DeriveAnalystRating(overview),
                RetrievedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch fundamentals for {Ticker}", ticker);
            return new FundamentalsData(ticker, null, null, null, null, null, null, null, DateTime.UtcNow);
        }
    }

    internal static double? ComputeDebtToEquity(AlphaVantageBalanceSheetResponse? balanceSheet)
    {
        var latest = balanceSheet?.AnnualReports?.FirstOrDefault();
        if (latest is null)
            return null;

        var liabilities = ParseDouble(latest.TotalLiabilities);
        var equity = ParseDouble(latest.TotalShareholderEquity);

        if (liabilities is null || equity is null || equity.Value == 0)
            return null;

        return liabilities.Value / equity.Value;
    }

    private async Task<AlphaVantageOverviewResponse?> FetchOverviewAsync(string ticker)
    {
        var url = $"/query?function=OVERVIEW&symbol={ticker}&apikey={_options.AlphaVantage.ApiKey}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AlphaVantageOverviewResponse>(json);
    }

    private async Task<AlphaVantageEarningsResponse?> FetchEarningsAsync(string ticker)
    {
        var url = $"/query?function=EARNINGS&symbol={ticker}&apikey={_options.AlphaVantage.ApiKey}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AlphaVantageEarningsResponse>(json);
    }

    private async Task<AlphaVantageBalanceSheetResponse?> FetchBalanceSheetAsync(string ticker)
    {
        var url = $"/query?function=BALANCE_SHEET&symbol={ticker}&apikey={_options.AlphaVantage.ApiKey}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AlphaVantageBalanceSheetResponse>(json);
    }

    internal static string? DeriveAnalystRating(AlphaVantageOverviewResponse? overview)
    {
        if (overview is null)
            return null;

        var strongBuy = ParseInt(overview.AnalystRatingStrongBuy);
        var buy = ParseInt(overview.AnalystRatingBuy);
        var hold = ParseInt(overview.AnalystRatingHold);
        var sell = ParseInt(overview.AnalystRatingSell);
        var strongSell = ParseInt(overview.AnalystRatingStrongSell);

        var total = strongBuy + buy + hold + sell + strongSell;
        if (total == 0)
            return null;

        var bullish = strongBuy + buy;
        var bearish = sell + strongSell;

        if (bullish > bearish && bullish > hold)
            return strongBuy >= buy ? "Strong Buy" : "Buy";

        if (bearish > bullish && bearish > hold)
            return strongSell >= sell ? "Strong Sell" : "Sell";

        return "Hold";
    }

    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "None" || value == "-")
            return null;

        return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static int ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "None" || value == "-")
            return 0;

        return int.TryParse(value, out var result) ? result : 0;
    }
}
