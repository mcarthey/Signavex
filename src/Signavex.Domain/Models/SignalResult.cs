namespace Signavex.Domain.Models;

/// <summary>
/// The result of evaluating a single signal against a stock or market condition.
/// Score is -1.0 (strongly bearish) to +1.0 (strongly bullish), 0 = neutral.
/// </summary>
public record SignalResult(
    string SignalName,
    double Score,
    double Weight,
    string Reason,
    bool IsAvailable
);
