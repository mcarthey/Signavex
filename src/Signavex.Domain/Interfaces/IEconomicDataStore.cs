using Signavex.Domain.Models.Economic;

namespace Signavex.Domain.Interfaces;

public interface IEconomicDataStore
{
    Task<IReadOnlyList<EconomicSeries>> GetAllSeriesAsync(bool enabledOnly = false, CancellationToken ct = default);
    Task<EconomicSeries?> GetSeriesByIdAsync(string seriesId, CancellationToken ct = default);
    Task<IReadOnlyList<EconomicObservation>> GetObservationsAsync(string seriesId, DateOnly? startDate = null, CancellationToken ct = default);
    Task UpsertObservationsAsync(string seriesId, IReadOnlyList<EconomicObservation> observations, CancellationToken ct = default);
    Task<EconomicSyncStatus?> GetSyncStatusAsync(string seriesId, CancellationToken ct = default);
    Task UpdateSyncTimestampAsync(string seriesId, int observationCount, CancellationToken ct = default);
}
