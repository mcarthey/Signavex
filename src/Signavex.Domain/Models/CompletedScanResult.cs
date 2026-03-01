namespace Signavex.Domain.Models;

/// <summary>
/// The full result of a completed scan, persisted to disk so results survive app restarts.
/// </summary>
public record CompletedScanResult(
    string ScanId,
    DateTime CompletedAtUtc,
    MarketContext MarketContext,
    IReadOnlyList<StockCandidate> Candidates,
    int TotalEvaluated,
    int ErrorCount
);
