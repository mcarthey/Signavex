namespace Signavex.Infrastructure.Persistence.Entities;

public class EconomicSeriesEntity
{
    public int Id { get; set; }
    public string SeriesId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string SeasonalAdjustment { get; set; } = string.Empty;
    public DateTime? LastUpdated { get; set; }
    public bool IsEnabled { get; set; }
    public int Category { get; set; }

    public List<EconomicObservationEntity> Observations { get; set; } = new();
}
