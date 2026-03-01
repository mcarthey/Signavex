namespace Signavex.Domain.Models;

/// <summary>
/// Result of a historical backtest scan run against a specific as-of date.
/// </summary>
public record BacktestResult(
    DateOnly AsOfDate,
    IReadOnlyList<StockCandidate> Candidates,
    MarketContext MarketContext,
    string? Caveat
);
