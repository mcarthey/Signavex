using Signavex.Domain.Analysis;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Worker;

public class DailyBriefBackgroundService : BackgroundService
{
    private readonly IScanStateStore _scanStateStore;
    private readonly IScanHistoryStore _scanHistoryStore;
    private readonly IEconomicDataStore _economicDataStore;
    private readonly IDailyBriefStore _briefStore;
    private readonly IAiBriefGenerator _briefGenerator;
    private readonly IScanCommandStore _commandStore;
    private readonly ILogger<DailyBriefBackgroundService> _logger;

    public const string CommandType = "GenerateBrief";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    public DailyBriefBackgroundService(
        IScanStateStore scanStateStore,
        IScanHistoryStore scanHistoryStore,
        IEconomicDataStore economicDataStore,
        IDailyBriefStore briefStore,
        IAiBriefGenerator briefGenerator,
        IScanCommandStore commandStore,
        ILogger<DailyBriefBackgroundService> logger)
    {
        _scanStateStore = scanStateStore;
        _scanHistoryStore = scanHistoryStore;
        _economicDataStore = economicDataStore;
        _briefStore = briefStore;
        _briefGenerator = briefGenerator;
        _commandStore = commandStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyBriefBackgroundService started — polling for GenerateBrief commands");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var command = await _commandStore.DequeueCommandAsync(CommandType, stoppingToken);
                if (command is not null)
                {
                    _logger.LogInformation("Brief generation requested (command {Id})", command.Id);
                    await GenerateBriefAsync(stoppingToken);
                    await _commandStore.CompleteCommandAsync(command.Id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing brief generation command");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task GenerateBriefAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Starting daily brief generation");
            var context = await BuildContextAsync(ct);

            var (title, content) = await _briefGenerator.GenerateDailyBriefAsync(context, ct);

            var brief = new DailyBrief(
                Id: 0,
                Date: context.Date,
                Title: title,
                Content: content,
                GeneratedAtUtc: DateTime.UtcNow,
                ScanId: context.LatestScan?.ScanId,
                EconomicHealthScore: context.EconomicHealth?.OverallScore,
                MarketOutlook: context.EconomicHealth?.ScoreLabel,
                CandidateCount: context.LatestScan?.Candidates.Count ?? 0);

            await _briefStore.SaveBriefAsync(brief, ct);
            _logger.LogInformation("Daily brief generated and saved: {Title}", title);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily brief — retrying once after 60s");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), ct);
                var context = await BuildContextAsync(ct);
                var (title, content) = await _briefGenerator.GenerateDailyBriefAsync(context, ct);

                var brief = new DailyBrief(
                    Id: 0,
                    Date: context.Date,
                    Title: title,
                    Content: content,
                    GeneratedAtUtc: DateTime.UtcNow,
                    ScanId: context.LatestScan?.ScanId,
                    EconomicHealthScore: context.EconomicHealth?.OverallScore,
                    MarketOutlook: context.EconomicHealth?.ScoreLabel,
                    CandidateCount: context.LatestScan?.Candidates.Count ?? 0);

                await _briefStore.SaveBriefAsync(brief, ct);
                _logger.LogInformation("Daily brief generated on retry: {Title}", title);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Daily brief generation failed on retry — skipping today");
            }
        }
    }

    private async Task<DailyBriefContext> BuildContextAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")));

        // Load scan data
        var latestScan = await _scanStateStore.LoadLatestResultAsync(ct);
        var recentScans = await _scanHistoryStore.GetRecentScansAsync(30, ct);
        var multiplierTrend = await _scanHistoryStore.GetMarketMultiplierTrendAsync(30, ct);

        // Run economic analysis (same logic as EconomicDashboardService)
        var allSeries = await _economicDataStore.GetAllSeriesAsync(enabledOnly: true, ct);
        var analysisService = new EconomicAnalysisService();
        var interpretations = new List<IndicatorInterpretation>();

        foreach (var series in allSeries)
        {
            var observations = await _economicDataStore.GetObservationsAsync(series.SeriesId, ct: ct);
            if (observations.Count < 2) continue;

            var interp = analysisService.AnalyzeIndicator(series, observations);
            interpretations.Add(interp);
        }

        var health = interpretations.Count > 0
            ? analysisService.CalculateEconomicHealth(interpretations)
            : null;

        var correlationService = new CorrelationAnalysisService();
        var correlations = interpretations.Count > 0
            ? correlationService.Analyze(interpretations)
            : null;

        ActionPlan? actionPlan = null;
        if (interpretations.Count > 0 && correlations is not null)
        {
            var recService = new RecommendationService();
            actionPlan = recService.GetRecommendations(
                UserProfile.General, interpretations, correlations);
        }

        return new DailyBriefContext(
            LatestScan: latestScan,
            RecentScans: recentScans,
            MultiplierTrend: multiplierTrend,
            EconomicHealth: health,
            Indicators: interpretations.AsReadOnly(),
            Correlations: correlations,
            ActionPlan: actionPlan,
            Date: today);
    }

}
