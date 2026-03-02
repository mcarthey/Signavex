namespace Signavex.Domain.Analysis;

public record ActionRecommendation(
    string Id,
    string Category,
    string Title,
    string Description,
    string Priority,       // "critical", "high", "medium", "low"
    string Timeframe,      // "immediate", "short-term", "medium-term"
    string Reasoning,
    IReadOnlyList<string> RelevantIndicators,
    IReadOnlyList<UserProfile> Profiles);

public record PortfolioAllocation(
    int Stocks,
    int Bonds,
    int Cash,
    int Alternatives);

public record ActionPlan(
    UserProfile Profile,
    IReadOnlyList<ActionRecommendation> Recommendations,
    string Summary,
    string EconomicOutlook);
