using System.Text.Json.Serialization;

namespace Signavex.Infrastructure.AlphaVantage.Dtos;

internal class AlphaVantageOverviewResponse
{
    [JsonPropertyName("Symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("PERatio")]
    public string? PERatio { get; set; }

    [JsonPropertyName("EPS")]
    public string? EPS { get; set; }

    [JsonPropertyName("AnalystTargetPrice")]
    public string? AnalystTargetPrice { get; set; }

    [JsonPropertyName("AnalystRatingStrongBuy")]
    public string? AnalystRatingStrongBuy { get; set; }

    [JsonPropertyName("AnalystRatingBuy")]
    public string? AnalystRatingBuy { get; set; }

    [JsonPropertyName("AnalystRatingHold")]
    public string? AnalystRatingHold { get; set; }

    [JsonPropertyName("AnalystRatingSell")]
    public string? AnalystRatingSell { get; set; }

    [JsonPropertyName("AnalystRatingStrongSell")]
    public string? AnalystRatingStrongSell { get; set; }
}
