namespace Signavex.Domain.Models;

/// <summary>
/// Open, High, Low, Close, Volume record for a single trading day.
/// </summary>
public record OhlcvRecord(
    string Ticker,
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume
);
