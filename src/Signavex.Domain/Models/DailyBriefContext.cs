using Signavex.Domain.Analysis;

namespace Signavex.Domain.Models;

public record DailyBriefContext(
    CompletedScanResult? LatestScan,
    IReadOnlyList<ScanSummary> RecentScans,
    IReadOnlyList<(DateTime Date, double Multiplier)> MultiplierTrend,
    EconomicHealthSummary? EconomicHealth,
    IReadOnlyList<IndicatorInterpretation> Indicators,
    CorrelationAnalysis? Correlations,
    ActionPlan? ActionPlan,
    DateOnly Date);
