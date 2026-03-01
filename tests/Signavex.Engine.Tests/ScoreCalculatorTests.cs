using Signavex.Domain.Models;

namespace Signavex.Engine.Tests;

public class ScoreCalculatorTests
{
    private readonly ScoreCalculator _calculator = new();

    [Fact]
    public void NoAvailableSignals_ReturnsZero()
    {
        var signals = new[]
        {
            new SignalResult("A", 1.0, 1.0, "reason", false),
            new SignalResult("B", -1.0, 1.0, "reason", false)
        };

        var score = _calculator.CalculateWeightedScore(signals);

        Assert.Equal(0, score);
    }

    [Fact]
    public void SingleSignal_ReturnsScore()
    {
        var signals = new[]
        {
            new SignalResult("A", 0.8, 1.0, "reason", true)
        };

        var score = _calculator.CalculateWeightedScore(signals);

        Assert.Equal(0.8, score, precision: 4);
    }

    [Fact]
    public void WeightedAverage_CalculatesCorrectly()
    {
        var signals = new[]
        {
            new SignalResult("A", 1.0, 2.0, "reason", true),
            new SignalResult("B", -1.0, 1.0, "reason", true)
        };

        // (1.0 * 2.0 + -1.0 * 1.0) / (2.0 + 1.0) = 1.0/3.0 ≈ 0.333
        var score = _calculator.CalculateWeightedScore(signals);

        Assert.Equal(1.0 / 3.0, score, precision: 4);
    }

    [Fact]
    public void ApplyMarketMultiplier_ScalesCorrectly()
    {
        var result = _calculator.ApplyMarketMultiplier(0.5, 1.3);

        Assert.Equal(0.65, result, precision: 4);
    }

    [Fact]
    public void MixedAvailability_OnlyCountsAvailable()
    {
        var signals = new[]
        {
            new SignalResult("A", 1.0, 1.5, "reason", true),
            new SignalResult("B", -1.0, 1.0, "reason", false),
            new SignalResult("C", 0.5, 1.0, "reason", true)
        };

        // Only A and C: (1.0*1.5 + 0.5*1.0) / (1.5+1.0) = 2.0/2.5 = 0.8
        var score = _calculator.CalculateWeightedScore(signals);

        Assert.Equal(0.8, score, precision: 4);
    }
}
