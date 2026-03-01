namespace Signavex.Domain.Models;

/// <summary>
/// Progress report emitted during a scan run.
/// </summary>
public record ScanProgress(
    int Evaluated,
    int Total,
    string CurrentTicker,
    int ErrorCount
);
