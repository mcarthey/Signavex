using Signavex.Domain.Models;
using Signavex.Signals.Market;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Market;

public class InterestRateSignalTests
{
    private readonly InterestRateSignal _signal = new(TestSignalOptionsFactory.CreateDefault());
    private static readonly IReadOnlyList<OhlcvRecord> EmptyOhlcv = Array.Empty<OhlcvRecord>();

    [Fact]
    public async Task NoRateData_ReturnsNotAvailable()
    {
        var macro = new MacroIndicators(null, null, 18.0, DateTime.UtcNow);
        var result = await _signal.EvaluateAsync(macro, EmptyOhlcv);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task FallingRate_ReturnsBullish()
    {
        var macro = new MacroIndicators(4.5, 5.0, 18.0, DateTime.UtcNow);
        var result = await _signal.EvaluateAsync(macro, EmptyOhlcv);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("falling", result.Reason);
    }

    [Fact]
    public async Task StableRate_ReturnsSlightlyPositive()
    {
        var macro = new MacroIndicators(5.0, 5.0, 18.0, DateTime.UtcNow);
        var result = await _signal.EvaluateAsync(macro, EmptyOhlcv);

        Assert.Equal(0.25, result.Score);
        Assert.Contains("stable", result.Reason);
    }

    [Fact]
    public async Task AggressiveHike_ReturnsBearish()
    {
        var macro = new MacroIndicators(5.5, 5.0, 18.0, DateTime.UtcNow);
        var result = await _signal.EvaluateAsync(macro, EmptyOhlcv);

        Assert.True(result.Score < 0);
        Assert.Contains("rate", result.Reason);
    }
}
