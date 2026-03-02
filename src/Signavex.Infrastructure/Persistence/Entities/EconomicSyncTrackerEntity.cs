namespace Signavex.Infrastructure.Persistence.Entities;

public class EconomicSyncTrackerEntity
{
    public int Id { get; set; }
    public string SeriesId { get; set; } = string.Empty;
    public DateTime LastSyncUtc { get; set; }
    public int ObservationCount { get; set; }
}
