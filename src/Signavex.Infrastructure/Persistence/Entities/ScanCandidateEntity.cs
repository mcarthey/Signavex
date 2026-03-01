namespace Signavex.Infrastructure.Persistence.Entities;

public class ScanCandidateEntity
{
    public int Id { get; set; }
    public int ScanRunId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int Tier { get; set; }
    public double RawScore { get; set; }
    public double FinalScore { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public string SignalResultsJson { get; set; } = string.Empty;

    public ScanRunEntity ScanRun { get; set; } = null!;
}
