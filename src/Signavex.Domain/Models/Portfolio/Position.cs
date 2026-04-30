namespace Signavex.Domain.Models.Portfolio;

/// <summary>
/// An open (currently-held) position in the portfolio backtest.
/// Money values are <see cref="decimal"/> to match <see cref="OhlcvRecord"/> price precision.
/// </summary>
public record Position(
    string Ticker,
    int Shares,
    decimal EntryPrice,
    DateOnly EntryDate,
    string? EntryReason,
    decimal StopLossPrice,
    decimal TakeProfitPrice
);
