using System.Text.Json.Serialization;

namespace Signavex.Infrastructure.Polygon.Dtos;

internal class PolygonTickerDetailsResponse
{
    [JsonPropertyName("results")]
    public PolygonTickerDetails? Results { get; set; }
}

internal class PolygonTickerDetails
{
    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("sic_description")]
    public string? SicDescription { get; set; }

    [JsonPropertyName("homepage_url")]
    public string? HomepageUrl { get; set; }

    [JsonPropertyName("market_cap")]
    public double? MarketCap { get; set; }

    [JsonPropertyName("total_employees")]
    public int? TotalEmployees { get; set; }

    [JsonPropertyName("branding")]
    public PolygonBranding? Branding { get; set; }
}

internal class PolygonBranding
{
    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; set; }
}
