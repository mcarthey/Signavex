using System.Text.Json.Serialization;

namespace Signavex.Infrastructure.Polygon.Dtos;

internal class PolygonAggregatesResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("resultsCount")]
    public int ResultsCount { get; set; }

    [JsonPropertyName("results")]
    public List<PolygonAggregateResult>? Results { get; set; }
}

internal class PolygonAggregateResult
{
    [JsonPropertyName("t")]
    public long Timestamp { get; set; }

    [JsonPropertyName("o")]
    public decimal Open { get; set; }

    [JsonPropertyName("h")]
    public decimal High { get; set; }

    [JsonPropertyName("l")]
    public decimal Low { get; set; }

    [JsonPropertyName("c")]
    public decimal Close { get; set; }

    [JsonPropertyName("v")]
    public double Volume { get; set; }
}
