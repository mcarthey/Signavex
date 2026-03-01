namespace Signavex.Domain.Configuration;

/// <summary>
/// Configurable weights for each Tier 2 stock-level signal.
/// Higher weight = stronger influence on the final stock score.
/// </summary>
public class SignalWeightsOptions
{
    public double VolumeThreshold { get; set; } = 1.0;
    public double MovingAverageCrossover { get; set; } = 1.5;
    public double SupportResistance { get; set; } = 1.2;
    public double TrendDirection { get; set; } = 1.5;
    public double ChannelPosition { get; set; } = 1.0;
    public double NewsSentiment { get; set; } = 1.3;
    public double AnalystRating { get; set; } = 1.2;
    public double PeRatioVsIndustry { get; set; } = 1.0;
    public double DebtEquityRatio { get; set; } = 0.8;
    public double EarningsTrend { get; set; } = 1.3;
}
