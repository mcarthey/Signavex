using Moq;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Engine.Tests;

public class MarketEvaluatorTests
{
    private readonly ScoreCalculator _calculator = new();

    [Fact]
    public async Task AllBullish_ReturnsHighMultiplier()
    {
        var signal1 = new Mock<IMarketSignal>();
        signal1.Setup(s => s.EvaluateAsync(It.IsAny<MacroIndicators>(), It.IsAny<IReadOnlyList<OhlcvRecord>>()))
            .ReturnsAsync(new SignalResult("S1", 1.0, 1.0, "bullish", true));

        var signal2 = new Mock<IMarketSignal>();
        signal2.Setup(s => s.EvaluateAsync(It.IsAny<MacroIndicators>(), It.IsAny<IReadOnlyList<OhlcvRecord>>()))
            .ReturnsAsync(new SignalResult("S2", 1.0, 1.0, "bullish", true));

        var evaluator = new MarketEvaluator(new[] { signal1.Object, signal2.Object }, _calculator);
        var macro = new MacroIndicators(5.0, 5.0, 15.0, DateTime.UtcNow);

        var context = await evaluator.EvaluateAsync(macro, Array.Empty<OhlcvRecord>());

        Assert.Equal(1.5, context.Multiplier);
        Assert.Contains("Bullish", context.Summary);
    }

    [Fact]
    public async Task AllBearish_ReturnsLowMultiplier()
    {
        var signal = new Mock<IMarketSignal>();
        signal.Setup(s => s.EvaluateAsync(It.IsAny<MacroIndicators>(), It.IsAny<IReadOnlyList<OhlcvRecord>>()))
            .ReturnsAsync(new SignalResult("S1", -1.0, 1.0, "bearish", true));

        var evaluator = new MarketEvaluator(new[] { signal.Object }, _calculator);
        var macro = new MacroIndicators(5.0, 5.0, 40.0, DateTime.UtcNow);

        var context = await evaluator.EvaluateAsync(macro, Array.Empty<OhlcvRecord>());

        Assert.Equal(0.5, context.Multiplier);
        Assert.Contains("Bearish", context.Summary);
    }

    [Fact]
    public async Task NeutralSignals_ReturnsMultiplierNearOne()
    {
        var signal = new Mock<IMarketSignal>();
        signal.Setup(s => s.EvaluateAsync(It.IsAny<MacroIndicators>(), It.IsAny<IReadOnlyList<OhlcvRecord>>()))
            .ReturnsAsync(new SignalResult("S1", 0.0, 1.0, "neutral", true));

        var evaluator = new MarketEvaluator(new[] { signal.Object }, _calculator);
        var macro = new MacroIndicators(5.0, 5.0, 20.0, DateTime.UtcNow);

        var context = await evaluator.EvaluateAsync(macro, Array.Empty<OhlcvRecord>());

        Assert.Equal(1.0, context.Multiplier);
        Assert.Contains("Neutral", context.Summary);
    }
}
