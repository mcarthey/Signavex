using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

public interface IAiBriefGenerator
{
    Task<(string Title, string Content)> GenerateDailyBriefAsync(
        DailyBriefContext context, CancellationToken ct = default);
}
