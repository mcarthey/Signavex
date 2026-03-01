namespace Signavex.Domain.Models;

/// <summary>
/// Parameter object passed to ScanEngine to resume a partially-completed scan.
/// The engine skips already-evaluated tickers and reuses the market context.
/// </summary>
public record ScanResumeState(
    MarketContext MarketContext,
    HashSet<string> AlreadyEvaluatedTickers,
    List<StockCandidate> CandidatesSoFar,
    int PriorErrorCount
);
