namespace Signavex.Domain.Models;

/// <summary>
/// The result of Tier 1 market-level evaluation.
/// The Multiplier (0.5–1.5) gates and scales all stock-level scores.
/// </summary>
public record MarketContext(
    double Multiplier,
    string Summary,
    IEnumerable<SignalResult> MarketSignals
);
