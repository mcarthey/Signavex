using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Web.Services;

public class DailyBriefService
{
    private readonly IDailyBriefStore _briefStore;
    private readonly IScanCommandStore _commandStore;

    public DailyBriefService(IDailyBriefStore briefStore, IScanCommandStore commandStore)
    {
        _briefStore = briefStore;
        _commandStore = commandStore;
    }

    public async Task<DailyBrief?> GetLatestBriefAsync(CancellationToken ct = default)
    {
        return await _briefStore.GetLatestBriefAsync(ct);
    }

    public async Task<DailyBrief?> GetBriefByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        return await _briefStore.GetBriefByDateAsync(date, ct);
    }

    public async Task<IReadOnlyList<DailyBrief>> GetRecentBriefsAsync(int count = 30, CancellationToken ct = default)
    {
        return await _briefStore.GetRecentBriefsAsync(count, ct);
    }

    public async Task RequestGenerationAsync(CancellationToken ct = default)
    {
        if (await _commandStore.HasPendingCommandAsync("GenerateBrief", ct))
            return;

        await _commandStore.EnqueueCommandAsync("GenerateBrief", ct);
    }

    public async Task<bool> HasPendingGenerationAsync(CancellationToken ct = default)
    {
        return await _commandStore.HasPendingCommandAsync("GenerateBrief", ct);
    }
}
