namespace Signavex.Domain.Models;

/// <summary>
/// Persisted state of an in-progress scan for crash recovery.
/// Saved to disk after each stock evaluation.
/// </summary>
public record ScanCheckpoint(
    string ScanId,
    DateTime StartedAtUtc,
    MarketContext MarketContext,
    IReadOnlyList<string> UniverseTickers,
    IReadOnlyList<string> EvaluatedTickers,
    IReadOnlyList<StockCandidate> CandidatesSoFar,
    int ErrorCount
);
