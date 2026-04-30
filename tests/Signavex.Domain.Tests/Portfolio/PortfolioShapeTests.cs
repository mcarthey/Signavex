using Signavex.Domain.Models.Portfolio;

namespace Signavex.Domain.Tests.Portfolio;

/// <summary>
/// Q1: domain shapes & contracts. These tests don't exercise simulation
/// logic — they just lock in the record contracts and helpers we'll
/// build the rest of Quantback on top of.
/// </summary>
public class PortfolioShapeTests
{
    [Fact]
    public void Trade_HoldDays_ComputesFromDates()
    {
        var trade = new Trade(
            Ticker: "AAPL",
            Shares: 10,
            EntryDate: new DateOnly(2024, 1, 1),
            EntryPrice: 100m,
            ExitDate: new DateOnly(2024, 1, 31),
            ExitPrice: 110m,
            ExitReason: TradeExitReason.TakeProfit,
            RealizedPnL: 100m);

        Assert.Equal(30, trade.HoldDays);
    }

    [Fact]
    public void Trade_ReturnPct_ComputesFromPrices()
    {
        var trade = new Trade("AAPL", 10,
            new DateOnly(2024, 1, 1), 100m,
            new DateOnly(2024, 1, 31), 110m,
            TradeExitReason.TakeProfit, 100m);

        Assert.Equal(0.10m, trade.ReturnPct);
    }

    [Fact]
    public void Trade_ReturnPct_ZeroEntryPrice_ReturnsZero()
    {
        var trade = new Trade("AAPL", 10,
            new DateOnly(2024, 1, 1), 0m,
            new DateOnly(2024, 1, 31), 0m,
            TradeExitReason.EndOfBacktest, 0m);

        Assert.Equal(0m, trade.ReturnPct);
    }

    [Fact]
    public void StrategyParameters_Default_HasReasonableValues()
    {
        var p = StrategyParameters.Default;

        Assert.Equal(0.05m, p.PositionSizePct);
        Assert.Equal(0.20m, p.MaxPerTickerPct);
        Assert.True(p.ExitOnSignalReversal);
        Assert.True(p.MinScoreToEnter > 0);
    }

    [Fact]
    public void PortfolioBacktestResult_Empty_RoundTripsRequest()
    {
        var request = new PortfolioBacktestRequest(
            StartDate: new DateOnly(2020, 1, 1),
            EndDate: new DateOnly(2025, 1, 1),
            StartingCapital: 100_000m,
            Universe: new[] { "AAPL", "MSFT" },
            Strategy: StrategyParameters.Default);

        var now = DateTime.UtcNow;
        var result = PortfolioBacktestResult.Empty(request, now);

        Assert.Same(request, result.Request);
        Assert.Empty(result.EquityCurve);
        Assert.Empty(result.Trades);
        Assert.Empty(result.OpenPositions);
        Assert.Equal(now, result.StartedAt);
        Assert.Equal(now, result.CompletedAt);
    }

    [Fact]
    public void PortfolioBacktestMetrics_Empty_PreservesStartingEquity()
    {
        var metrics = PortfolioBacktestMetrics.Empty(50_000m);

        Assert.Equal(50_000m, metrics.StartingEquity);
        Assert.Equal(50_000m, metrics.EndingEquity);
        Assert.Equal(0, metrics.TotalTrades);
        Assert.Equal(0.0, metrics.SharpeRatio);
    }

    [Fact]
    public void Records_ImplementValueEquality()
    {
        var a = new EquityPoint(new DateOnly(2024, 1, 1), 100m, 50m, 150m, 1);
        var b = new EquityPoint(new DateOnly(2024, 1, 1), 100m, 50m, 150m, 1);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
