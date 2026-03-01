namespace Signavex.Domain.Models;

public record ScanSummary(
    string ScanId,
    DateTime CompletedAtUtc,
    double MarketMultiplier,
    string MarketSummary,
    int CandidateCount,
    int TotalEvaluated,
    int ErrorCount
);
