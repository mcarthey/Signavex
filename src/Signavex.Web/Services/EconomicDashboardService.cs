using Signavex.Domain.Analysis;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models.Economic;

namespace Signavex.Web.Services;

public class EconomicDashboardService
{
    private readonly IEconomicDataStore _dataStore;
    private readonly IScanCommandStore _commandStore;

    private EconomicHealthSummary? _cachedHealth;
    private CorrelationAnalysis? _cachedCorrelations;
    private IReadOnlyList<IndicatorInterpretation>? _cachedInterpretations;
    private DateTime _cacheTime;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public EconomicDashboardService(IEconomicDataStore dataStore, IScanCommandStore commandStore)
    {
        _dataStore = dataStore;
        _commandStore = commandStore;
    }

    public async Task RequestSyncAsync(CancellationToken ct = default)
    {
        if (await _commandStore.HasPendingCommandAsync("EconomicSync", ct))
            return;

        await _commandStore.EnqueueCommandAsync("EconomicSync", ct);
    }

    public async Task<bool> HasPendingSyncAsync(CancellationToken ct = default)
    {
        return await _commandStore.HasPendingCommandAsync("EconomicSync", ct);
    }

    public void InvalidateCache()
    {
        _cacheTime = DateTime.MinValue;
    }

    public async Task<IReadOnlyList<IndicatorInterpretation>> GetIndicatorInterpretationsAsync(
        CancellationToken ct = default)
    {
        await RefreshCacheIfNeeded(ct);
        return _cachedInterpretations ?? Array.Empty<IndicatorInterpretation>();
    }

    public async Task<EconomicHealthSummary?> GetHealthSummaryAsync(CancellationToken ct = default)
    {
        await RefreshCacheIfNeeded(ct);
        return _cachedHealth;
    }

    public async Task<CorrelationAnalysis?> GetCorrelationsAsync(CancellationToken ct = default)
    {
        await RefreshCacheIfNeeded(ct);
        return _cachedCorrelations;
    }

    public async Task<ActionPlan> GetRecommendationsAsync(
        UserProfile profile, CancellationToken ct = default)
    {
        await RefreshCacheIfNeeded(ct);

        var recs = new RecommendationService();
        return recs.GetRecommendations(
            profile,
            _cachedInterpretations ?? Array.Empty<IndicatorInterpretation>(),
            _cachedCorrelations ?? new CorrelationAnalysis(Array.Empty<CorrelationPattern>(),
                "No data available", "low"));
    }

    public async Task<(EconomicSeries Series, IReadOnlyList<EconomicObservation> Observations)?> GetIndicatorDetailAsync(
        string seriesId, CancellationToken ct = default)
    {
        var series = await _dataStore.GetSeriesByIdAsync(seriesId, ct);
        if (series is null) return null;

        var observations = await _dataStore.GetObservationsAsync(seriesId, ct: ct);
        return (series, observations);
    }

    private async Task RefreshCacheIfNeeded(CancellationToken ct)
    {
        if (_cachedInterpretations is not null && DateTime.UtcNow - _cacheTime < CacheDuration)
            return;

        var allSeries = await _dataStore.GetAllSeriesAsync(enabledOnly: true, ct);
        var analysisService = new EconomicAnalysisService();
        var interpretations = new List<IndicatorInterpretation>();

        foreach (var series in allSeries)
        {
            var observations = await _dataStore.GetObservationsAsync(series.SeriesId, ct: ct);
            if (observations.Count < 2) continue;

            var interp = analysisService.AnalyzeIndicator(series, observations);
            interpretations.Add(interp);
        }

        _cachedInterpretations = interpretations.AsReadOnly();
        _cachedHealth = interpretations.Count > 0
            ? analysisService.CalculateEconomicHealth(interpretations)
            : null;

        var correlationService = new CorrelationAnalysisService();
        _cachedCorrelations = interpretations.Count > 0
            ? correlationService.Analyze(interpretations)
            : null;

        _cacheTime = DateTime.UtcNow;
    }
}
