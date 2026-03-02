namespace Signavex.Domain.Models.Economic;

public record EconomicObservation(
    string SeriesId,
    DateOnly Date,
    double Value);
