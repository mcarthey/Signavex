namespace Signavex.Domain.Models.Economic;

public record EconomicSeries(
    string SeriesId,
    string Name,
    string Description,
    string Frequency,
    string Units,
    string SeasonalAdjustment,
    DateTime? LastUpdated,
    bool IsEnabled,
    EconomicCategory Category);
