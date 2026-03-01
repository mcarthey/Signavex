namespace Signavex.Infrastructure.Persistence.Entities;

public class ScanCheckpointEntity
{
    public int Id { get; set; }
    public string ScanId { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}
