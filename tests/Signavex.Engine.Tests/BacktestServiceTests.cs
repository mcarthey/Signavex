using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Signavex.Domain.Configuration;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Engine.Tests;

public class BacktestServiceTests
{
    private static IOptions<SignavexOptions> DefaultOptions =>
        Options.Create(new SignavexOptions
        {
            SurfacingThreshold = 0.65,
            Universe = new List<string> { "SP500" }
        });

    private static List<OhlcvRecord> CreateOhlcvHistory(string ticker, int days)
    {
        return Enumerable.Range(0, days)
            .Select(i => new OhlcvRecord(ticker, DateOnly.FromDateTime(DateTime.Today.AddDays(-days + i)),
                100m, 105m, 95m, 100m, 5_000_000L))
            .ToList();
    }

    private (BacktestService Service, Mock<IMarketDataProvider> MarketData) CreateService(
        Mock<IMarketDataProvider>? marketData = null,
        Mock<IStockSignal>? stockSignal = null,
        Mock<IMarketSignal>? marketSignal = null)
    {
        marketData ??= new Mock<IMarketDataProvider>();
        stockSignal ??= new Mock<IStockSignal>();
        marketSignal ??= new Mock<IMarketSignal>();

        var economicProvider = new Mock<IEconomicDataProvider>();
        economicProvider.Setup(e => e.GetMacroIndicatorsAsync())
            .ReturnsAsync(new MacroIndicators(5.0, 5.0, 15.0, DateTime.UtcNow));

        if (marketSignal.Setups.Count == 0)
        {
            marketSignal.Setup(s => s.EvaluateAsync(It.IsAny<MacroIndicators>(), It.IsAny<IReadOnlyList<OhlcvRecord>>()))
                .ReturnsAsync(new SignalResult("MockMarket", 0.5, 1.0, "neutral-positive", true));
        }

        var calculator = new ScoreCalculator();
        var marketEvaluator = new MarketEvaluator(new[] { marketSignal.Object }, calculator);
        var stockEvaluator = new StockEvaluator(new[] { stockSignal.Object }, calculator, DefaultOptions);
        var universeProvider = new UniverseProvider(marketData.Object, DefaultOptions);

        var service = new BacktestService(
            marketData.Object, economicProvider.Object, marketEvaluator,
            stockEvaluator, universeProvider, DefaultOptions, NullLogger<BacktestService>.Instance);

        return (service, marketData);
    }

    [Fact]
    public async Task RunBacktest_TrimsOhlcvToAsOfDate()
    {
        // Full history includes records through today; as-of date is 30 days ago
        var asOfDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var fullHistory = CreateOhlcvHistory("AAPL", 500);

        IReadOnlyList<OhlcvRecord>? capturedOhlcv = null;

        var stockSignal = new Mock<IStockSignal>();
        stockSignal.Setup(s => s.EvaluateAsync(It.IsAny<StockData>()))
            .Callback<StockData>(sd => capturedOhlcv = sd.OhlcvHistory)
            .ReturnsAsync(new SignalResult("Mock", 0.8, 1.0, "bullish", true));

        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetDailyOhlcvAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(fullHistory);
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(new[] { "AAPL" });

        var (service, _) = CreateService(marketData: marketData, stockSignal: stockSignal);

        await service.RunBacktestAsync(asOfDate);

        // The OHLCV data passed to the stock evaluator should be trimmed
        Assert.NotNull(capturedOhlcv);
        Assert.True(capturedOhlcv!.All(r => r.Date <= asOfDate));
        Assert.True(capturedOhlcv.Count < fullHistory.Count);
    }

    [Fact]
    public async Task RunBacktest_SurfacesCandidatesAboveThreshold()
    {
        var asOfDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var ohlcv = CreateOhlcvHistory("SPY", 500);

        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetDailyOhlcvAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(ohlcv);
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(new[] { "AAPL" });

        var stockSignal = new Mock<IStockSignal>();
        stockSignal.Setup(s => s.EvaluateAsync(It.IsAny<StockData>()))
            .ReturnsAsync(new SignalResult("MockSignal", 0.8, 1.0, "bullish", true));

        var (service, _) = CreateService(marketData: marketData, stockSignal: stockSignal);

        var result = await service.RunBacktestAsync(asOfDate);

        Assert.NotEmpty(result.Candidates);
        Assert.Equal(asOfDate, result.AsOfDate);
        Assert.Equal("AAPL", result.Candidates[0].Ticker);
    }

    [Fact]
    public async Task RunBacktest_SetsAsOfDateAndCaveat()
    {
        var asOfDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-60));
        var ohlcv = CreateOhlcvHistory("SPY", 500);

        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetDailyOhlcvAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(ohlcv);
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(Array.Empty<string>());

        var (service, _) = CreateService(marketData: marketData);

        var result = await service.RunBacktestAsync(asOfDate);

        Assert.Equal(asOfDate, result.AsOfDate);
        Assert.NotNull(result.Caveat);
        Assert.Contains("News and fundamental data", result.Caveat);
    }

    [Fact]
    public async Task RunBacktest_SkipsFailedStocks()
    {
        var asOfDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var ohlcv = CreateOhlcvHistory("SPY", 500);

        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetDailyOhlcvAsync("SPY", It.IsAny<int>()))
            .ReturnsAsync(ohlcv);
        marketData.Setup(m => m.GetDailyOhlcvAsync("FAIL", It.IsAny<int>()))
            .ThrowsAsync(new HttpRequestException("API error"));
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(new[] { "FAIL" });

        var (service, _) = CreateService(marketData: marketData);

        var result = await service.RunBacktestAsync(asOfDate);

        Assert.Empty(result.Candidates);
        Assert.Equal(asOfDate, result.AsOfDate);
    }
}
