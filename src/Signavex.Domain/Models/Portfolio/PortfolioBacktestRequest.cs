namespace Signavex.Domain.Models.Portfolio;

/// <summary>
/// Inputs to a portfolio-simulation backtest. Distinct from the existing
/// point-in-time <see cref="BacktestResult"/> which answers "what would
/// have surfaced on date X?"; this answers "what would 5 years of
/// mechanically following the picks have done to my equity?".
/// </summary>
public record PortfolioBacktestRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal StartingCapital,
    IReadOnlyList<string> Universe,
    StrategyParameters Strategy
);
