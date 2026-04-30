namespace Signavex.Domain.Models.Portfolio;

/// <summary>
/// Mechanical-strategy rules applied each simulated trading day.
/// All percentage fields are unit-fractions (0.05 = 5%), matching how
/// <see cref="SignalResult"/> scores are expressed.
/// </summary>
public record StrategyParameters(
    decimal PositionSizePct,
    decimal MaxPerTickerPct,
    decimal StopLossPct,
    decimal TakeProfitPct,
    bool ExitOnSignalReversal,
    double MinScoreToEnter
)
{
    public static StrategyParameters Default => new(
        PositionSizePct: 0.05m,
        MaxPerTickerPct: 0.20m,
        StopLossPct: 0.08m,
        TakeProfitPct: 0.20m,
        ExitOnSignalReversal: true,
        MinScoreToEnter: 0.45);
}
