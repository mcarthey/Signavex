namespace Signavex.Domain.Models;

/// <summary>
/// Fundamental financial data for a stock. Refreshed quarterly.
/// </summary>
public record FundamentalsData(
    string Ticker,
    double? PeRatio,
    double? IndustryPeRatio,
    double? DebtToEquityRatio,
    double? EpsCurrentQuarter,
    double? EpsEstimateCurrentQuarter,
    double? EpsPreviousYear,
    string? AnalystRating,
    DateTime RetrievedAt
);
