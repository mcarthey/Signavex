using System.Text.Json.Serialization;

namespace Signavex.Infrastructure.AlphaVantage.Dtos;

internal class AlphaVantageBalanceSheetResponse
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("annualReports")]
    public List<AlphaVantageBalanceSheetReport>? AnnualReports { get; set; }
}

internal class AlphaVantageBalanceSheetReport
{
    [JsonPropertyName("fiscalDateEnding")]
    public string? FiscalDateEnding { get; set; }

    [JsonPropertyName("totalLiabilities")]
    public string? TotalLiabilities { get; set; }

    [JsonPropertyName("totalShareholderEquity")]
    public string? TotalShareholderEquity { get; set; }
}
