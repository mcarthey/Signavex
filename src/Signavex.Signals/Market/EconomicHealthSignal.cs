using Microsoft.Extensions.Options;
using Signavex.Domain.Analysis;
using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;

namespace Signavex.Signals.Market;

/// <summary>
/// Tier 1: Economic health signal — uses economic analysis service
/// to produce a score from the overall economic health (0-100).
/// Maps economic health to a -1 to +1 signal score.
/// </summary>
public sealed class EconomicHealthSignal : IMarketSignal
{
    private readonly IEconomicDataStore _dataStore;
    private readonly MarketSignalWeightsOptions _weights;

    public EconomicHealthSignal(
        IEconomicDataStore dataStore,
        IOptions<SignavexOptions> options)
    {
        _dataStore = dataStore;
        _weights = options.Value.MarketSignalWeights;
    }

    public string Name => "EconomicHealth";
    public double DefaultWeight => _weights.EconomicHealth;

    public async Task<SignalResult> EvaluateAsync(MacroIndicators indicators, IReadOnlyList<OhlcvRecord> spOhlcv)
    {
        try
        {
            var allSeries = await _dataStore.GetAllSeriesAsync(enabledOnly: true);
            if (allSeries.Count == 0)
                return new SignalResult(Name, 0, DefaultWeight, "No economic data available", false);

            var analysisService = new EconomicAnalysisService();
            var interpretations = new List<IndicatorInterpretation>();

            foreach (var series in allSeries)
            {
                var observations = await _dataStore.GetObservationsAsync(series.SeriesId);
                if (observations.Count < 2) continue;

                var interp = analysisService.AnalyzeIndicator(series, observations);
                interpretations.Add(interp);
            }

            if (interpretations.Count == 0)
                return new SignalResult(Name, 0, DefaultWeight, "Insufficient economic data for analysis", false);

            var health = analysisService.CalculateEconomicHealth(interpretations);

            // Map 0-100 health score to -1 to +1:
            // 80+ → 0.5 to 1.0 (bullish)
            // 50-80 → -0.25 to 0.5 (neutral to mildly bullish)
            // 20-50 → -0.75 to -0.25 (bearish)
            // 0-20 → -1.0 to -0.75 (strongly bearish)
            double score = (health.OverallScore - 50.0) / 50.0;
            score = Math.Clamp(score, -1.0, 1.0);

            return new SignalResult(Name, score, DefaultWeight,
                $"Economic health: {health.ScoreLabel} ({health.OverallScore}/100)", true);
        }
        catch
        {
            return new SignalResult(Name, 0, DefaultWeight, "Economic health evaluation failed", false);
        }
    }
}
