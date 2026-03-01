namespace Signavex.Domain.Models;

/// <summary>
/// Extended progress report with ETA and resume information.
/// Inherits from ScanProgress so existing consumers continue to work.
/// </summary>
public record EnhancedScanProgress(
    int Evaluated,
    int Total,
    string CurrentTicker,
    int ErrorCount,
    int CandidatesFound,
    bool IsResuming,
    TimeSpan? EstimatedTimeRemaining
) : ScanProgress(Evaluated, Total, CurrentTicker, ErrorCount);
