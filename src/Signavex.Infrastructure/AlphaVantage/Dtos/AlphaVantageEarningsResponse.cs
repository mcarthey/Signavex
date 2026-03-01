using System.Text.Json.Serialization;

namespace Signavex.Infrastructure.AlphaVantage.Dtos;

internal class AlphaVantageEarningsResponse
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("quarterlyEarnings")]
    public List<AlphaVantageQuarterlyEarning>? QuarterlyEarnings { get; set; }
}

internal class AlphaVantageQuarterlyEarning
{
    [JsonPropertyName("reportedEPS")]
    public string? ReportedEPS { get; set; }

    [JsonPropertyName("estimatedEPS")]
    public string? EstimatedEPS { get; set; }

    [JsonPropertyName("fiscalDateEnding")]
    public string? FiscalDateEnding { get; set; }
}
