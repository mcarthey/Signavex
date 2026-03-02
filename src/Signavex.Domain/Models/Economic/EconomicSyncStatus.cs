namespace Signavex.Domain.Models.Economic;

public record EconomicSyncStatus(
    string SeriesId,
    DateTime LastSyncUtc,
    int ObservationCount);
