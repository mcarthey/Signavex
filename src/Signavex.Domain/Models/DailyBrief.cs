namespace Signavex.Domain.Models;

public record DailyBrief(
    int Id,
    DateOnly Date,
    string Title,
    string Content,
    DateTime GeneratedAtUtc,
    string? ScanId,
    int? EconomicHealthScore,
    string? MarketOutlook,
    int CandidateCount);
