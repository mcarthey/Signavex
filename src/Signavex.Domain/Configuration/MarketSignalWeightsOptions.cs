namespace Signavex.Domain.Configuration;

/// <summary>
/// Configurable weights for each Tier 1 market-level signal.
/// These feed into the MarketContext multiplier calculation.
/// </summary>
public class MarketSignalWeightsOptions
{
    public double MarketTrend { get; set; } = 2.0;
    public double InterestRateEnvironment { get; set; } = 1.5;
    public double VixLevel { get; set; } = 1.5;
    public double SectorMomentum { get; set; } = 1.0;
}
