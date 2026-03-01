namespace Signavex.Domain.Models;

/// <summary>
/// Return type from ScanEngine.RunScanAsync — includes market context and error count
/// alongside the surfaced candidates.
/// </summary>
public record ScanRunResult(
    MarketContext MarketContext,
    IReadOnlyList<StockCandidate> Candidates,
    int TotalEvaluated,
    int ErrorCount
);
