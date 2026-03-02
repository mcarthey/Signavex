using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

public interface IDailyBriefStore
{
    Task SaveBriefAsync(DailyBrief brief, CancellationToken ct = default);
    Task<DailyBrief?> GetLatestBriefAsync(CancellationToken ct = default);
    Task<DailyBrief?> GetBriefByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<DailyBrief>> GetRecentBriefsAsync(int count = 30, CancellationToken ct = default);
}
