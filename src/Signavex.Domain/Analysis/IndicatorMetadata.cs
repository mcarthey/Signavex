using Signavex.Domain.Models.Economic;

namespace Signavex.Domain.Analysis;

public record IndicatorMetadataEntry(
    string SeriesId,
    EconomicCategory Category,
    int Priority,
    int DisplayOrder,
    string GoodDirection, // "up" or "down"
    double? TargetMin,
    double? TargetMax,
    string WhatItMeans,
    string WhyItMatters,
    string GoodRange,
    string WhatDrivesThis,
    string ImplicationsRising,
    string ImplicationsFalling,
    double? PreCovidAvg,
    double? HistoricalAvg,
    double? RecessionLevel,
    double? ExpansionLevel,
    string? Suffix,
    int Decimals,
    bool IsCurrency);

public static class IndicatorMetadata
{
    private static readonly Dictionary<string, IndicatorMetadataEntry> _registry = new(StringComparer.OrdinalIgnoreCase);

    static IndicatorMetadata()
    {
        Register(new("UNRATE", EconomicCategory.Employment, 1, 1, "down", 3.5, 5.0,
            "Percentage of people actively looking for work who cannot find jobs",
            "Low unemployment indicates a strong job market and healthy economy",
            "3.5-5.0% is considered healthy full employment",
            "Job creation/losses, labor force participation, economic growth, business confidence, and seasonal factors",
            "Job losses, weakening economy, reduced consumer spending power, potential recession signal",
            "Job creation, economic expansion, increased consumer spending, but if too low may cause wage inflation",
            3.7, 5.7, 8.0, 4.0, "%", 1, false));

        Register(new("PAYEMS", EconomicCategory.Employment, 2, 2, "up", null, null,
            "Total number of employees on nonfarm payrolls (in thousands)",
            "Rising payrolls indicate job creation and economic expansion",
            "Steady growth of 150,000+ jobs per month is healthy",
            "Business expansion/contraction, economic growth, consumer demand, productivity, and automation trends",
            "Job creation, economic growth, increased income and spending, tightening labor market",
            "Layoffs, business contraction, recession risk, reduced consumer spending power",
            152000, 140000, 130000, 160000, "K", 0, false));

        Register(new("CPIAUCSL", EconomicCategory.Inflation, 1, 1, "down", 1.5, 2.5,
            "Measures average change in prices paid by urban consumers for goods and services",
            "High inflation erodes purchasing power; too low can signal weak demand",
            "Fed targets around 2% annual increase",
            "Supply/demand balance, energy prices, wage growth, monetary policy, global supply chains",
            "Eroding purchasing power, potential Fed rate hikes, reduced consumer spending, higher cost of living",
            "Increased purchasing power, potential Fed rate cuts, but if too low may signal weak demand or deflation risk",
            252, 230, 240, 255, null, 2, false));

        Register(new("PPIACO", EconomicCategory.Inflation, 3, 2, "down", null, null,
            "Measures average change in prices received by domestic producers",
            "Leading indicator for consumer inflation; shows upstream price pressures",
            "Moderate growth around 2% annually",
            "Raw material costs, energy prices, labor costs, global commodity prices, supply chain efficiency",
            "Producer cost pressures, likely future consumer price increases, margin compression for businesses",
            "Easing input costs, potential consumer price relief, improving business margins",
            195, 180, 185, 200, null, 2, false));

        Register(new("GDPC1", EconomicCategory.Growth, 1, 1, "up", null, null,
            "Total value of all goods and services produced, adjusted for inflation",
            "The primary measure of economic growth and overall economic health",
            "Growth of 2-3% annually is considered healthy",
            "Consumer spending, business investment, government spending, exports minus imports, productivity gains",
            "Economic expansion, job creation, business growth, rising incomes, increased tax revenues",
            "Economic contraction, potential recession, job losses, declining corporate profits",
            19000, 17500, 18000, 19500, "B", 2, true));

        Register(new("INDPRO", EconomicCategory.Growth, 2, 2, "up", null, null,
            "Measures output of manufacturing, mining, and utilities sectors",
            "Key indicator of industrial sector health and economic activity",
            "Steady positive growth indicates expanding production",
            "Manufacturing demand, capacity utilization, new orders, inventory levels, export demand",
            "Strong manufacturing sector, business expansion, increased hiring, supply meeting demand",
            "Weak manufacturing, potential layoffs, reduced business investment, weakening economic activity",
            109, 100, 95, 110, null, 2, false));

        Register(new("FEDFUNDS", EconomicCategory.InterestRates, 1, 1, "down", null, null,
            "Interest rate at which banks lend to each other overnight",
            "Fed uses this to control inflation and stimulate/cool the economy",
            "Depends on economic conditions; higher to fight inflation, lower to boost growth",
            "Federal Reserve policy decisions based on inflation, unemployment, and economic growth targets",
            "Fed fighting inflation, higher borrowing costs, slowing economic activity, stronger dollar, potential housing market cooling",
            "Fed stimulating economy, lower borrowing costs, encouraging investment and spending, weaker dollar",
            1.75, 3.5, 0.25, 2.5, "%", 2, false));

        Register(new("GS10", EconomicCategory.InterestRates, 2, 2, "down", null, null,
            "Yield on 10-year U.S. Treasury bonds",
            "Benchmark for mortgage rates and corporate borrowing; reflects economic expectations",
            "Varies with inflation expectations and economic conditions",
            "Inflation expectations, Fed policy, economic growth outlook, global demand for safe assets, government borrowing",
            "Higher borrowing costs, mortgage rates up, bond prices down, often signals economic growth or inflation fears",
            "Lower borrowing costs, mortgage rates down, bond prices up, often signals economic uncertainty or recession fears",
            2.0, 4.5, 1.5, 3.0, "%", 2, false));

        Register(new("SP500", EconomicCategory.Market, 1, 3, "up", null, null,
            "Stock market index of 500 largest U.S. public companies",
            "Reflects investor confidence and corporate profitability",
            "Steady long-term growth with moderate volatility",
            "Corporate earnings, economic growth, interest rates, investor sentiment, geopolitical events, Fed policy",
            "Strong investor confidence, positive economic outlook, wealth effect boosts spending, rising retirement accounts",
            "Investor uncertainty, economic concerns, potential recession fears, declining household wealth",
            3230, 2500, 2200, 3500, null, 2, false));

        Register(new("RECPROUSM156N", EconomicCategory.Market, 2, 4, "down", null, null,
            "Probability that U.S. economy is in recession based on yield curve",
            "Early warning indicator for potential economic downturns",
            "Below 20% is low risk; above 30% signals elevated recession risk",
            "Yield curve shape (spread between long and short-term rates), Fed policy, economic growth expectations",
            "Increased recession risk, inverted yield curve, potential economic slowdown ahead, flight to safety",
            "Lower recession risk, normal yield curve, positive economic outlook, investor confidence improving",
            5, 15, 60, 10, "%", 1, false));

        Register(new("MORTGAGE30US", EconomicCategory.Housing, 1, 1, "down", null, null,
            "Average interest rate on 30-year fixed-rate mortgages",
            "Directly impacts home affordability and housing market activity",
            "Below 4% historically favorable; above 7% significantly impacts affordability",
            "10-year Treasury yields, Fed policy, inflation expectations, housing demand, lender risk assessments",
            "Reduced home affordability, slower housing market, fewer refinancings, potential price corrections",
            "Improved affordability, increased home buying, refinancing boom, potential housing price increases",
            3.9, 6.0, 3.5, 4.5, "%", 2, false));

        Register(new("PCE", EconomicCategory.Consumer, 1, 1, "up", null, null,
            "Total spending by consumers on goods and services",
            "Consumer spending drives ~70% of U.S. economy; key growth indicator",
            "Steady growth indicates healthy consumer confidence",
            "Employment levels, wage growth, consumer confidence, credit availability, savings rates, wealth effects",
            "Strong consumer confidence, economic growth, business expansion, job creation, but may fuel inflation",
            "Weak consumer confidence, economic slowdown, potential recession, business contraction, deflation risk",
            14500, 13000, 13500, 15000, "B", 2, true));
    }

    private static void Register(IndicatorMetadataEntry entry) => _registry[entry.SeriesId] = entry;

    public static IndicatorMetadataEntry? Get(string seriesId) =>
        _registry.TryGetValue(seriesId, out var entry) ? entry : null;

    public static IReadOnlyDictionary<string, IndicatorMetadataEntry> All => _registry;
}
