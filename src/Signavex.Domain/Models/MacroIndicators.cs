namespace Signavex.Domain.Models;

/// <summary>
/// Macro-economic indicators used by Tier 1 market-level signals.
/// </summary>
public record MacroIndicators(
    double? FedFundsRate,
    double? FedFundsRatePreviousMonth,
    double? VixLevel,
    DateTime RetrievedAt
);
