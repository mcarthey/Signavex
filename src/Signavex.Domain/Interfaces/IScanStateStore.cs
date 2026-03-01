using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

/// <summary>
/// Persists scan state (checkpoints and completed results) to durable storage.
/// </summary>
public interface IScanStateStore
{
    Task SaveCheckpointAsync(ScanCheckpoint checkpoint, CancellationToken ct = default);
    Task<ScanCheckpoint?> LoadCheckpointAsync(CancellationToken ct = default);
    Task DeleteCheckpointAsync(CancellationToken ct = default);

    Task SaveCompletedResultAsync(CompletedScanResult result, CancellationToken ct = default);
    Task<CompletedScanResult?> LoadLatestResultAsync(CancellationToken ct = default);
}
