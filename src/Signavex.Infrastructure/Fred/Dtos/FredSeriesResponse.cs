using System.Text.Json.Serialization;

namespace Signavex.Infrastructure.Fred.Dtos;

internal class FredSeriesResponse
{
    [JsonPropertyName("observations")]
    public List<FredObservation>? Observations { get; set; }
}

internal class FredObservation
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
