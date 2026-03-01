using System.Text.Json.Serialization;

namespace Signavex.Infrastructure.Polygon.Dtos;

internal class PolygonNewsResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("results")]
    public List<PolygonNewsResult>? Results { get; set; }
}

internal class PolygonNewsResult
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("published_utc")]
    public string? PublishedUtc { get; set; }

    [JsonPropertyName("tickers")]
    public List<string>? Tickers { get; set; }

    [JsonPropertyName("publisher")]
    public PolygonPublisher? Publisher { get; set; }
}

internal class PolygonPublisher
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
