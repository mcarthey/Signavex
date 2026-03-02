namespace Signavex.Infrastructure.Persistence.Entities;

public class DailyBriefEntity
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public string? ScanId { get; set; }
    public int? EconomicHealthScore { get; set; }
    public string? MarketOutlook { get; set; }
    public int CandidateCount { get; set; }
}
