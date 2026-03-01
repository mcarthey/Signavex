using Signavex.Domain.Models;
using Signavex.Signals.Market;
using Signavex.Signals.Tests.Helpers;

namespace Signavex.Signals.Tests.Market;

public class VixLevelSignalTests
{
    private readonly VixLevelSignal _signal = new(TestSignalOptionsFactory.CreateDefault());
    private static readonly IReadOnlyList<OhlcvRecord> EmptyOhlcv = Array.Empty<OhlcvRecord>();

    [Fact]
    public async Task NoVixData_ReturnsNotAvailable()
    {
        var macro = new MacroIndicators(5.0, 5.0, null, DateTime.UtcNow);
        var result = await _signal.EvaluateAsync(macro, EmptyOhlcv);

        Assert.Equal(0, result.Score);
        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task LowVix_ReturnsPositiveOne()
    {
        var macro = new MacroIndicators(5.0, 5.0, 12.0, DateTime.UtcNow);
        var result = await _signal.EvaluateAsync(macro, EmptyOhlcv);

        Assert.Equal(1.0, result.Score);
        Assert.Contains("very low", result.Reason);
    }

    [Fact]
    public async Task ModerateVix_ReturnsZero()
    {
        var macro = new MacroIndicators(5.0, 5.0, 23.0, DateTime.UtcNow);
        var result = await _signal.EvaluateAsync(macro, EmptyOhlcv);

        Assert.Equal(0.0, result.Score);
        Assert.Contains("moderate", result.Reason);
    }

    [Fact]
    public async Task ExtremeVix_ReturnsNegativeOne()
    {
        var macro = new MacroIndicators(5.0, 5.0, 45.0, DateTime.UtcNow);
        var result = await _signal.EvaluateAsync(macro, EmptyOhlcv);

        Assert.Equal(-1.0, result.Score);
        Assert.Contains("extreme", result.Reason);
    }
}
