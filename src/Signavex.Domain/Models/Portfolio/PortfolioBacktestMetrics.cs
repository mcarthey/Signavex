namespace Signavex.Domain.Models.Portfolio;

/// <summary>
/// Summary statistics computed from a completed portfolio backtest.
/// All ratio/percentage fields are <see cref="double"/> for math friendliness;
/// money values stay <see cref="decimal"/>.
/// </summary>
public record PortfolioBacktestMetrics(
    decimal StartingEquity,
    decimal EndingEquity,
    double TotalReturnPct,
    double AnnualizedReturnPct,
    double SharpeRatio,
    double MaxDrawdownPct,
    int TotalTrades,
    int WinningTrades,
    int LosingTrades,
    double WinRate,
    decimal AvgWinPnL,
    decimal AvgLossPnL,
    double AvgHoldDays
)
{
    public static PortfolioBacktestMetrics Empty(decimal startingEquity) => new(
        StartingEquity: startingEquity,
        EndingEquity: startingEquity,
        TotalReturnPct: 0,
        AnnualizedReturnPct: 0,
        SharpeRatio: 0,
        MaxDrawdownPct: 0,
        TotalTrades: 0,
        WinningTrades: 0,
        LosingTrades: 0,
        WinRate: 0,
        AvgWinPnL: 0,
        AvgLossPnL: 0,
        AvgHoldDays: 0);
}
