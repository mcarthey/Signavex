using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

public interface IScanCommandStore
{
    Task EnqueueCommandAsync(string commandType, CancellationToken ct = default);
    Task<ScanCommand?> DequeueCommandAsync(CancellationToken ct = default);
    Task CompleteCommandAsync(int commandId, CancellationToken ct = default);
    Task<bool> HasPendingCommandAsync(CancellationToken ct = default);
}
