namespace Signavex.Domain.Models;

public record ScanCommand(
    int Id,
    string CommandType,
    DateTime RequestedAtUtc,
    DateTime? PickedUpAtUtc,
    DateTime? CompletedAtUtc
);
