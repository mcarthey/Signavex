using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Signavex.Domain.Configuration;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Engine.Tests;

public class ScanEngineResumeTests
{
    private static IOptions<SignavexOptions> DefaultOptions =>
        Options.Create(new SignavexOptions
        {
            SurfacingThreshold = 0.65,
            Universe = new List<string> { "SP500" }
        });

    private static IReadOnlyList<OhlcvRecord> CreateOhlcv(string ticker) =>
        Enumerable.Range(0, 250)
            .Select(i => new OhlcvRecord(ticker, DateOnly.FromDateTime(DateTime.Today.AddDays(-250 + i)),
                400m, 405m, 395m, 400m, 10_000_000L))
            .ToList().AsReadOnly();

    private static MarketContext CreateMarketContext() =>
        new(1.2, "Bullish", new[] { new SignalResult("MarketTrend", 0.8, 2.0, "uptrend", true) });

    private ScanEngine CreateEngine(
        Mock<IMarketDataProvider> marketData,
        Mock<INewsDataProvider> newsProvider,
        Mock<IFundamentalsProvider> fundamentalsProvider,
        Mock<IEconomicDataProvider> economicProvider,
        Mock<IStockSignal> stockSignal,
        Mock<IMarketSignal> marketSignal)
    {
        var calculator = new ScoreCalculator();
        var marketEvaluator = new MarketEvaluator(new[] { marketSignal.Object }, calculator);
        var stockEvaluator = new StockEvaluator(new[] { stockSignal.Object }, calculator, DefaultOptions);
        var universeProvider = new UniverseProvider(marketData.Object, DefaultOptions);

        return new ScanEngine(
            marketData.Object, newsProvider.Object, fundamentalsProvider.Object,
            economicProvider.Object, marketEvaluator, stockEvaluator, universeProvider,
            NullLogger<ScanEngine>.Instance);
    }

    [Fact]
    public async Task RunScan_WithResumeState_SkipsAlreadyEvaluatedTickers()
    {
        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(new[] { "AAPL", "MSFT" });
        // Only set up MSFT — AAPL should be skipped
        marketData.Setup(m => m.GetDailyOhlcvAsync("MSFT", It.IsAny<int>()))
            .ReturnsAsync(CreateOhlcv("MSFT"));

        var newsProvider = new Mock<INewsDataProvider>();
        newsProvider.Setup(n => n.GetRecentNewsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<NewsItem>());

        var fundamentalsProvider = new Mock<IFundamentalsProvider>();
        fundamentalsProvider.Setup(f => f.GetFundamentalsAsync(It.IsAny<string>()))
            .ReturnsAsync(new FundamentalsData("MSFT", 15.0, 20.0, 0.5, 2.0, 1.8, 1.5, "Buy", DateTime.UtcNow));

        var economicProvider = new Mock<IEconomicDataProvider>();
        var stockSignal = new Mock<IStockSignal>();
        stockSignal.Setup(s => s.EvaluateAsync(It.IsAny<StockData>()))
            .ReturnsAsync(new SignalResult("Mock", 0.8, 1.0, "bullish", true));

        var marketSignal = new Mock<IMarketSignal>();

        var engine = CreateEngine(marketData, newsProvider, fundamentalsProvider, economicProvider, stockSignal, marketSignal);

        var resumeState = new ScanResumeState(
            CreateMarketContext(),
            new HashSet<string> { "AAPL" },
            new List<StockCandidate>(),
            0);

        var result = await engine.RunScanAsync(null, resumeState, null);

        // AAPL was skipped — should NOT have been called for OHLCV
        marketData.Verify(m => m.GetDailyOhlcvAsync("AAPL", It.IsAny<int>()), Times.Never);
        // MSFT was evaluated
        marketData.Verify(m => m.GetDailyOhlcvAsync("MSFT", It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task RunScan_WithResumeState_UsesProvidedMarketContext()
    {
        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(Array.Empty<string>()); // Empty universe — just testing market context

        var economicProvider = new Mock<IEconomicDataProvider>();
        var newsProvider = new Mock<INewsDataProvider>();
        var fundamentalsProvider = new Mock<IFundamentalsProvider>();
        var stockSignal = new Mock<IStockSignal>();
        var marketSignal = new Mock<IMarketSignal>();

        var engine = CreateEngine(marketData, newsProvider, fundamentalsProvider, economicProvider, stockSignal, marketSignal);

        var resumeState = new ScanResumeState(
            CreateMarketContext(),
            new HashSet<string>(),
            new List<StockCandidate>(),
            0);

        var result = await engine.RunScanAsync(null, resumeState, null);

        // Should NOT call economic provider — market context was provided
        economicProvider.Verify(e => e.GetMacroIndicatorsAsync(), Times.Never);
        // Should reuse provided market context
        Assert.Equal(1.2, result.MarketContext.Multiplier);
    }

    [Fact]
    public async Task RunScan_WithResumeState_IncludesPriorCandidates()
    {
        var priorCandidate = new StockCandidate("AAPL", "Apple", MarketTier.SP500,
            0.75, 0.80,
            new[] { new SignalResult("V", 0.6, 1.0, "ok", true) },
            CreateMarketContext(), DateTime.UtcNow);

        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(new[] { "AAPL" }); // AAPL in universe but already evaluated

        var economicProvider = new Mock<IEconomicDataProvider>();
        var newsProvider = new Mock<INewsDataProvider>();
        var fundamentalsProvider = new Mock<IFundamentalsProvider>();
        var stockSignal = new Mock<IStockSignal>();
        var marketSignal = new Mock<IMarketSignal>();

        var engine = CreateEngine(marketData, newsProvider, fundamentalsProvider, economicProvider, stockSignal, marketSignal);

        var resumeState = new ScanResumeState(
            CreateMarketContext(),
            new HashSet<string> { "AAPL" },
            new List<StockCandidate> { priorCandidate },
            2);

        var result = await engine.RunScanAsync(null, resumeState, null);

        Assert.Single(result.Candidates);
        Assert.Equal("AAPL", result.Candidates[0].Ticker);
    }

    [Fact]
    public async Task RunScan_CallsOnStockEvaluatedForEachStock()
    {
        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetDailyOhlcvAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(CreateOhlcv("X"));
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(new[] { "AAPL", "MSFT" });

        var newsProvider = new Mock<INewsDataProvider>();
        newsProvider.Setup(n => n.GetRecentNewsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<NewsItem>());

        var fundamentalsProvider = new Mock<IFundamentalsProvider>();
        fundamentalsProvider.Setup(f => f.GetFundamentalsAsync(It.IsAny<string>()))
            .ReturnsAsync(new FundamentalsData("X", 15.0, 20.0, 0.5, 2.0, 1.8, 1.5, "Hold", DateTime.UtcNow));

        var economicProvider = new Mock<IEconomicDataProvider>();
        economicProvider.Setup(e => e.GetMacroIndicatorsAsync())
            .ReturnsAsync(new MacroIndicators(5.0, 5.0, 15.0, DateTime.UtcNow));

        var stockSignal = new Mock<IStockSignal>();
        stockSignal.Setup(s => s.EvaluateAsync(It.IsAny<StockData>()))
            .ReturnsAsync(new SignalResult("Mock", 0.3, 1.0, "neutral", true));

        var marketSignal = new Mock<IMarketSignal>();
        marketSignal.Setup(s => s.EvaluateAsync(It.IsAny<MacroIndicators>(), It.IsAny<IReadOnlyList<OhlcvRecord>>()))
            .ReturnsAsync(new SignalResult("M", 0.0, 1.0, "neutral", true));

        var engine = CreateEngine(marketData, newsProvider, fundamentalsProvider, economicProvider, stockSignal, marketSignal);

        var evaluatedTickers = new List<string>();
        Func<string, StockCandidate?, Task> callback = (ticker, _) =>
        {
            evaluatedTickers.Add(ticker);
            return Task.CompletedTask;
        };

        await engine.RunScanAsync(null, null, callback);

        Assert.Equal(2, evaluatedTickers.Count);
        Assert.Contains("AAPL", evaluatedTickers);
        Assert.Contains("MSFT", evaluatedTickers);
    }
}
