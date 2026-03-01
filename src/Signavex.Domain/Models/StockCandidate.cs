using Signavex.Domain.Enums;

namespace Signavex.Domain.Models;

/// <summary>
/// A stock that has passed the surfacing threshold after full signal evaluation.
/// Contains the complete signal breakdown for dashboard display.
/// </summary>
public record StockCandidate(
    string Ticker,
    string CompanyName,
    MarketTier Tier,
    double RawScore,
    double FinalScore,
    IEnumerable<SignalResult> SignalResults,
    MarketContext MarketContext,
    DateTime EvaluatedAt
);
