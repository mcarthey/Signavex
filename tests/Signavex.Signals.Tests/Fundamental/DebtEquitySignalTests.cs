using Signavex.Domain.Models;
using Signavex.Signals.Fundamental;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Fundamental;

public class DebtEquitySignalTests
{
    private readonly DebtEquitySignal _signal = new(TestSignalOptionsFactory.CreateDefault());

    private static StockData MakeStock(double? deRatio) =>
        new("TEST", "Test Corp",
            Array.Empty<OhlcvRecord>(),
            new FundamentalsData("TEST", null, null, deRatio, null, null, null, null, DateTime.UtcNow),
            Array.Empty<NewsItem>());

    [Fact]
    public async Task NoData_ReturnsZeroNotAvailable()
    {
        var stock = MakeStock(null);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task LowDebt_ReturnsPositiveOne()
    {
        var stock = MakeStock(0.2);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("low (favorable)", result.Reason);
    }

    [Fact]
    public async Task ModerateDebt_ReturnsZero()
    {
        var stock = MakeStock(0.8);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(0.0, result.Score);
        Assert.Contains("moderate", result.Reason);
    }

    [Fact]
    public async Task HighDebt_ReturnsNegativeOne()
    {
        var stock = MakeStock(2.5);
        var result = await _signal.EvaluateAsync(stock);

        Assert.Equal(-1.0, result.Score);
        Assert.Contains("high (risk factor)", result.Reason);
    }
}
