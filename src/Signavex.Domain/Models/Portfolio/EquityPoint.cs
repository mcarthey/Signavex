namespace Signavex.Domain.Models.Portfolio;

/// <summary>
/// Single point on the equity curve. <see cref="TotalEquity"/> = <see cref="Cash"/> + <see cref="PositionsValue"/>.
/// </summary>
public record EquityPoint(
    DateOnly Date,
    decimal Cash,
    decimal PositionsValue,
    decimal TotalEquity,
    int OpenPositionCount
);
