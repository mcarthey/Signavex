using Microsoft.Extensions.Options;
using Moq;
using Signavex.Domain.Configuration;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Engine.Tests;

public class StockEvaluatorTests
{
    private readonly ScoreCalculator _calculator = new();

    private static IOptions<SignavexOptions> DefaultOptions =>
        Options.Create(new SignavexOptions { SurfacingThreshold = 0.65 });

    private static StockData TestStock => new("TEST", "Test Corp",
        Array.Empty<OhlcvRecord>(), null, Array.Empty<NewsItem>());

    private static MarketContext NeutralMarket => new(1.0, "Neutral", Array.Empty<SignalResult>());

    [Fact]
    public async Task AboveThreshold_ReturnsCandidateWithCorrectScores()
    {
        var signal = new Mock<IStockSignal>();
        signal.Setup(s => s.EvaluateAsync(It.IsAny<StockData>()))
            .ReturnsAsync(new SignalResult("Test", 0.8, 1.0, "bullish", true));

        var evaluator = new StockEvaluator(new[] { signal.Object }, _calculator, DefaultOptions);
        var result = await evaluator.EvaluateAsync(TestStock, NeutralMarket, MarketTier.SP500);

        Assert.NotNull(result);
        Assert.Equal("TEST", result.Ticker);
        Assert.Equal(0.8, result.RawScore, precision: 4);
        Assert.Equal(0.8, result.FinalScore, precision: 4);
    }

    [Fact]
    public async Task BelowThreshold_StillReturnsCandidateForStorageAndFiltering()
    {
        var signal = new Mock<IStockSignal>();
        signal.Setup(s => s.EvaluateAsync(It.IsAny<StockData>()))
            .ReturnsAsync(new SignalResult("Test", 0.3, 1.0, "weak", true));

        var evaluator = new StockEvaluator(new[] { signal.Object }, _calculator, DefaultOptions);
        var result = await evaluator.EvaluateAsync(TestStock, NeutralMarket, MarketTier.SP500);

        Assert.NotNull(result);
        Assert.Equal(0.3, result.RawScore, precision: 4);
        Assert.Equal(0.3, result.FinalScore, precision: 4);
    }

    [Fact]
    public async Task MarketMultiplierApplied()
    {
        var signal = new Mock<IStockSignal>();
        signal.Setup(s => s.EvaluateAsync(It.IsAny<StockData>()))
            .ReturnsAsync(new SignalResult("Test", 0.6, 1.0, "moderate", true));

        var bullishMarket = new MarketContext(1.5, "Bullish", Array.Empty<SignalResult>());
        var evaluator = new StockEvaluator(new[] { signal.Object }, _calculator, DefaultOptions);
        var result = await evaluator.EvaluateAsync(TestStock, bullishMarket, MarketTier.SP500);

        Assert.NotNull(result);
        Assert.Equal(0.6, result.RawScore, precision: 4);
        Assert.Equal(0.9, result.FinalScore, precision: 4); // 0.6 * 1.5
    }

    [Fact]
    public async Task AllTiers_ReturnCandidateRegardlessOfScore()
    {
        var signal = new Mock<IStockSignal>();
        signal.Setup(s => s.EvaluateAsync(It.IsAny<StockData>()))
            .ReturnsAsync(new SignalResult("Test", 0.7, 1.0, "good", true));

        var evaluator = new StockEvaluator(new[] { signal.Object }, _calculator, DefaultOptions);

        // Both tiers return candidates — threshold filtering happens at display time
        var sp500Result = await evaluator.EvaluateAsync(TestStock, NeutralMarket, MarketTier.SP500);
        var sp600Result = await evaluator.EvaluateAsync(TestStock, NeutralMarket, MarketTier.SP600);

        Assert.NotNull(sp500Result);
        Assert.NotNull(sp600Result);
        Assert.Equal(0.7, sp500Result.FinalScore, precision: 4);
        Assert.Equal(0.7, sp600Result.FinalScore, precision: 4);
    }
}
