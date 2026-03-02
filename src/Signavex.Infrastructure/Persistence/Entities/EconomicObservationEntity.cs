namespace Signavex.Infrastructure.Persistence.Entities;

public class EconomicObservationEntity
{
    public string SeriesId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public double Value { get; set; }

    public EconomicSeriesEntity? Series { get; set; }
}
