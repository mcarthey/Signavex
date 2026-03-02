namespace Signavex.Domain.Models;

public record ScanStatus(
    bool IsScanning,
    int Evaluated,
    int Total,
    string CurrentTicker,
    int CandidatesFound,
    int ErrorCount,
    DateTime? StartedAtUtc,
    DateTime? LastUpdatedAtUtc
);
