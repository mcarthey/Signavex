namespace Signavex.Infrastructure.Persistence.Entities;

public class ScanRunEntity
{
    public int Id { get; set; }
    public string ScanId { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; }
    public double MarketMultiplier { get; set; }
    public string MarketSummary { get; set; } = string.Empty;
    public string MarketSignalsJson { get; set; } = string.Empty;
    public int TotalEvaluated { get; set; }
    public int ErrorCount { get; set; }
    public int CandidateCount { get; set; }

    public List<ScanCandidateEntity> Candidates { get; set; } = new();
}
