namespace Signavex.Domain.Models.Portfolio;

/// <summary>
/// A closed trade in the portfolio backtest — entry through exit.
/// </summary>
public record Trade(
    string Ticker,
    int Shares,
    DateOnly EntryDate,
    decimal EntryPrice,
    DateOnly ExitDate,
    decimal ExitPrice,
    TradeExitReason ExitReason,
    decimal RealizedPnL
)
{
    public int HoldDays => ExitDate.DayNumber - EntryDate.DayNumber;
    public decimal ReturnPct => EntryPrice == 0 ? 0 : (ExitPrice - EntryPrice) / EntryPrice;
}

public enum TradeExitReason
{
    StopLoss,
    TakeProfit,
    SignalReversal,
    EndOfBacktest
}
