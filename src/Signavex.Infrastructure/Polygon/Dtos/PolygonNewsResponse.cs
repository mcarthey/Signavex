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

    [JsonPropertyName("insights")]
    public List<PolygonNewsInsight>? Insights { get; set; }
}

internal class PolygonPublisher
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal class PolygonNewsInsight
{
    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("sentiment")]
    public string? Sentiment { get; set; }

    [JsonPropertyName("sentiment_reasoning")]
    public string? SentimentReasoning { get; set; }
}
