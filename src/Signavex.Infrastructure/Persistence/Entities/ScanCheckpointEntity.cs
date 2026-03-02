namespace Signavex.Infrastructure.Persistence.Entities;

public class ScanCheckpointEntity
{
    public int Id { get; set; }
    public string ScanId { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }

    // Scalar progress columns for lightweight polling (no JSON deserialization needed)
    public int Evaluated { get; set; }
    public int Total { get; set; }
    public string CurrentTicker { get; set; } = string.Empty;
    public int CandidatesFound { get; set; }
    public int ErrorCount { get; set; }
    public bool IsActive { get; set; }
}
