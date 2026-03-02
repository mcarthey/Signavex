using Signavex.Domain.Models.Economic;

namespace Signavex.Domain.Interfaces;

public interface IFredApiClient
{
    Task<IReadOnlyList<EconomicObservation>> GetObservationsAsync(string seriesId, DateOnly? startDate = null, CancellationToken ct = default);
}
