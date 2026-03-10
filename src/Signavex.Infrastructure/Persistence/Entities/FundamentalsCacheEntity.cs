namespace Signavex.Infrastructure.Persistence.Entities;

public class FundamentalsCacheEntity
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public double? PeRatio { get; set; }
    public double? IndustryPeRatio { get; set; }
    public double? DebtToEquityRatio { get; set; }
    public double? EpsCurrentQuarter { get; set; }
    public double? EpsEstimateCurrentQuarter { get; set; }
    public double? EpsPreviousYear { get; set; }
    public string? AnalystRating { get; set; }
    public DateTime RetrievedAtUtc { get; set; }
}
