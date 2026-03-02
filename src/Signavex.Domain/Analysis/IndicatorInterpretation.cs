using Signavex.Domain.Models.Economic;

namespace Signavex.Domain.Analysis;

public record IndicatorInterpretation(
    string SeriesId,
    string Name,
    string Description,
    EconomicCategory Category,
    double? CurrentValue,
    double? PreviousValue,
    string TrendDirection,   // "up", "down", "stable"
    double ChangePercent,
    string Sentiment,        // "positive", "negative", "neutral"
    string Severity,         // "strong", "moderate", "mild"
    string CurrentAssessment,
    string Implications,
    string? BenchmarkComparison,
    string FormattedValue,
    string FormattedChange);

public record CategoryHealth(
    string CategoryName,
    EconomicCategory Category,
    double Score,
    string Trend,  // "up", "down", "stable"
    IReadOnlyList<IndicatorInterpretation> Indicators);

public record KeyInsight(
    string Type,   // "positive", "negative", "neutral"
    string Title,
    string Message,
    IReadOnlyList<string> Indicators);

public record EconomicHealthSummary(
    int OverallScore,
    string ScoreLabel,
    IReadOnlyList<CategoryHealth> CategoryScores,
    IReadOnlyList<KeyInsight> KeyInsights,
    DateTime LastUpdated);
