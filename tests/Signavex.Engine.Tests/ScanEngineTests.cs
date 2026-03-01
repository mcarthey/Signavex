using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Signavex.Domain.Configuration;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Engine.Tests;

public class ScanEngineTests
{
    private static IOptions<SignavexOptions> DefaultOptions =>
        Options.Create(new SignavexOptions
        {
            SurfacingThreshold = 0.65,
            Universe = new List<string> { "SP500" }
        });

    [Fact]
    public async Task RunScan_SurfacesCandidatesAboveThreshold()
    {
        var ohlcv = Enumerable.Range(0, 250)
            .Select(i => new OhlcvRecord("SPY", DateOnly.FromDateTime(DateTime.Today.AddDays(-250 + i)),
                400m, 405m, 395m, 400m, 10_000_000L))
            .ToList().AsReadOnly();

        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetDailyOhlcvAsync("SPY", It.IsAny<int>()))
            .ReturnsAsync(ohlcv);
        marketData.Setup(m => m.GetDailyOhlcvAsync("AAPL", It.IsAny<int>()))
            .ReturnsAsync(ohlcv);
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(new[] { "AAPL" });

        var newsProvider = new Mock<INewsDataProvider>();
        newsProvider.Setup(n => n.GetRecentNewsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<NewsItem>());

        var fundamentalsProvider = new Mock<IFundamentalsProvider>();
        fundamentalsProvider.Setup(f => f.GetFundamentalsAsync(It.IsAny<string>()))
            .ReturnsAsync(new FundamentalsData("AAPL", 15.0, 20.0, 0.5, 2.0, 1.8, 1.5, "Buy", DateTime.UtcNow));

        var economicProvider = new Mock<IEconomicDataProvider>();
        economicProvider.Setup(e => e.GetMacroIndicatorsAsync())
            .ReturnsAsync(new MacroIndicators(5.0, 5.0, 15.0, DateTime.UtcNow));

        // Use real stock signal that returns a high score
        var stockSignal = new Mock<IStockSignal>();
        stockSignal.Setup(s => s.EvaluateAsync(It.IsAny<StockData>()))
            .ReturnsAsync(new SignalResult("MockSignal", 0.8, 1.0, "bullish", true));

        var marketSignal = new Mock<IMarketSignal>();
        marketSignal.Setup(s => s.EvaluateAsync(It.IsAny<MacroIndicators>(), It.IsAny<IReadOnlyList<OhlcvRecord>>()))
            .ReturnsAsync(new SignalResult("MockMarket", 0.5, 1.0, "neutral-positive", true));

        var calculator = new ScoreCalculator();
        var marketEvaluator = new MarketEvaluator(new[] { marketSignal.Object }, calculator);
        var stockEvaluator = new StockEvaluator(new[] { stockSignal.Object }, calculator, DefaultOptions);
        var universeProvider = new UniverseProvider(marketData.Object, DefaultOptions);

        var engine = new ScanEngine(
            marketData.Object, newsProvider.Object, fundamentalsProvider.Object,
            economicProvider.Object, marketEvaluator, stockEvaluator, universeProvider,
            NullLogger<ScanEngine>.Instance);

        var result = await engine.RunScanAsync();

        Assert.Single(result.Candidates);
        Assert.Equal("AAPL", result.Candidates[0].Ticker);
        Assert.True(result.Candidates[0].FinalScore >= 0.65);
    }

    [Fact]
    public async Task RunScan_SkipsFailedStocks()
    {
        var ohlcv = Enumerable.Range(0, 250)
            .Select(i => new OhlcvRecord("SPY", DateOnly.FromDateTime(DateTime.Today.AddDays(-250 + i)),
                400m, 405m, 395m, 400m, 10_000_000L))
            .ToList().AsReadOnly();

        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetDailyOhlcvAsync("SPY", It.IsAny<int>()))
            .ReturnsAsync(ohlcv);
        marketData.Setup(m => m.GetDailyOhlcvAsync("FAIL", It.IsAny<int>()))
            .ThrowsAsync(new HttpRequestException("API error"));
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(new[] { "FAIL" });

        var newsProvider = new Mock<INewsDataProvider>();
        var fundamentalsProvider = new Mock<IFundamentalsProvider>();
        var economicProvider = new Mock<IEconomicDataProvider>();
        economicProvider.Setup(e => e.GetMacroIndicatorsAsync())
            .ReturnsAsync(new MacroIndicators(5.0, 5.0, 15.0, DateTime.UtcNow));

        var marketSignal = new Mock<IMarketSignal>();
        marketSignal.Setup(s => s.EvaluateAsync(It.IsAny<MacroIndicators>(), It.IsAny<IReadOnlyList<OhlcvRecord>>()))
            .ReturnsAsync(new SignalResult("M", 0.0, 1.0, "neutral", true));

        var stockSignal = new Mock<IStockSignal>();

        var calculator = new ScoreCalculator();
        var marketEvaluator = new MarketEvaluator(new[] { marketSignal.Object }, calculator);
        var stockEvaluator = new StockEvaluator(new[] { stockSignal.Object }, calculator, DefaultOptions);
        var universeProvider = new UniverseProvider(marketData.Object, DefaultOptions);

        var engine = new ScanEngine(
            marketData.Object, newsProvider.Object, fundamentalsProvider.Object,
            economicProvider.Object, marketEvaluator, stockEvaluator, universeProvider,
            NullLogger<ScanEngine>.Instance);

        var result = await engine.RunScanAsync();

        Assert.Empty(result.Candidates);
    }
}
