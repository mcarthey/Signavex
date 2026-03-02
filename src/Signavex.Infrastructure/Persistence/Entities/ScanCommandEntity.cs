namespace Signavex.Infrastructure.Persistence.Entities;

public class ScanCommandEntity
{
    public int Id { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? PickedUpAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
