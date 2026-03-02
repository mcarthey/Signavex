namespace Signavex.Domain.Analysis;

public record CorrelationPattern(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<string> Indicators,
    int Confidence,        // 0-100
    string Severity,       // "low", "medium", "high", "critical"
    string Type,           // "positive", "negative", "neutral"
    string EconomicSignal,
    string HistoricalContext);

public record CorrelationAnalysis(
    IReadOnlyList<CorrelationPattern> Patterns,
    string Summary,
    string OverallRisk);   // "low", "medium", "high", "critical"
