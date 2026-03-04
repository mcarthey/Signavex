using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Signavex.Domain.Configuration;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Signavex.Engine;

namespace Signavex.Worker.Tests;

public class WorkerScanOrchestratorTests
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

    /// <summary>
    /// Snapshot of checkpoint data captured at save time.
    /// The orchestrator passes ReadOnlyCollection wrappers around live mutable lists,
    /// so we must copy the data to observe the state at each save point.
    /// </summary>
    private record CheckpointSnapshot(
        IReadOnlyList<string> UniverseTickers,
        IReadOnlyList<string> EvaluatedTickers,
        int TotalOverride);

    private static (WorkerScanOrchestrator Orchestrator, List<CheckpointSnapshot> Snapshots)
        CreateOrchestrator(string[] universeTickers)
    {
        var marketData = new Mock<IMarketDataProvider>();
        marketData.Setup(m => m.GetIndexConstituentsAsync(MarketIndex.SP500))
            .ReturnsAsync(universeTickers);
        marketData.Setup(m => m.GetDailyOhlcvAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(CreateOhlcv("X"));

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

        var calculator = new ScoreCalculator();
        var marketEvaluator = new MarketEvaluator(new[] { marketSignal.Object }, calculator);
        var stockEvaluator = new StockEvaluator(new[] { stockSignal.Object }, calculator, DefaultOptions);
        var universeProvider = new UniverseProvider(marketData.Object, DefaultOptions);
        var engine = new ScanEngine(
            marketData.Object, newsProvider.Object, fundamentalsProvider.Object,
            economicProvider.Object, marketEvaluator, stockEvaluator, universeProvider,
            NullLogger<ScanEngine>.Instance);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(ScanEngine))).Returns(engine);
        serviceProvider.Setup(sp => sp.GetService(typeof(UniverseProvider))).Returns(universeProvider);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        // Snapshot checkpoint data at each save (copies lists to freeze state)
        var snapshots = new List<CheckpointSnapshot>();

        var stateStore = new Mock<IScanStateStore>();
        stateStore.Setup(s => s.LoadCheckpointAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScanCheckpoint?)null);
        stateStore.Setup(s => s.SaveCheckpointAsync(
                It.IsAny<ScanCheckpoint>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<ScanCheckpoint, int, CancellationToken>((cp, total, _) =>
                snapshots.Add(new CheckpointSnapshot(
                    cp.UniverseTickers.ToList().AsReadOnly(),
                    cp.EvaluatedTickers.ToList().AsReadOnly(),
                    total)))
            .Returns(Task.CompletedTask);
        stateStore.Setup(s => s.SaveCompletedResultAsync(
                It.IsAny<CompletedScanResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        stateStore.Setup(s => s.DeleteCheckpointAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var commandStore = new Mock<IScanCommandStore>();
        commandStore.Setup(c => c.EnqueueCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var orchestrator = new WorkerScanOrchestrator(
            scopeFactory.Object, stateStore.Object, commandStore.Object,
            NullLogger<WorkerScanOrchestrator>.Instance);

        return (orchestrator, snapshots);
    }

    [Fact]
    public async Task RunScan_CheckpointUniverseTickers_ContainsFullUniverse()
    {
        var universeTickers = new[] { "AAPL", "MSFT", "GOOG" };
        var (orchestrator, snapshots) = CreateOrchestrator(universeTickers);

        await orchestrator.RunScanAsync();

        // One checkpoint saved per evaluated ticker
        Assert.Equal(universeTickers.Length, snapshots.Count);

        // Every checkpoint should have UniverseTickers = full universe (3 tickers)
        foreach (var snapshot in snapshots)
        {
            Assert.Equal(universeTickers.Length, snapshot.UniverseTickers.Count);
            Assert.All(universeTickers, t => Assert.Contains(t, snapshot.UniverseTickers));
        }

        // EvaluatedTickers should grow incrementally: 1, 2, 3
        Assert.Single(snapshots[0].EvaluatedTickers);
        Assert.Equal(2, snapshots[1].EvaluatedTickers.Count);
        Assert.Equal(3, snapshots[2].EvaluatedTickers.Count);

        // First checkpoint: universe has 3 tickers, but only 1 evaluated
        // This is the core fix — before the bug, UniverseTickers would have had 1 item
        Assert.Equal(3, snapshots[0].UniverseTickers.Count);
        Assert.Single(snapshots[0].EvaluatedTickers);
    }

    [Fact]
    public async Task RunScan_CheckpointTotalOverride_MatchesUniverseCount()
    {
        var universeTickers = new[] { "AAPL", "MSFT" };
        var (orchestrator, snapshots) = CreateOrchestrator(universeTickers);

        await orchestrator.RunScanAsync();

        // totalOverride in saved checkpoints should match universe size (2)
        // Even if Progress<T> callback hasn't fired yet, the fallback uses
        // UniverseTickers.Count which now correctly reflects the full universe
        foreach (var snapshot in snapshots)
        {
            var effectiveTotal = snapshot.TotalOverride > 0
                ? snapshot.TotalOverride
                : snapshot.UniverseTickers.Count;
            Assert.Equal(2, effectiveTotal);
        }
    }
}
