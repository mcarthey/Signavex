namespace Signavex.Domain.Analysis;

public class CorrelationAnalysisService
{
    public CorrelationAnalysis Analyze(IReadOnlyList<IndicatorInterpretation> indicators)
    {
        var patterns = new List<CorrelationPattern>();

        IndicatorInterpretation? Get(string id) =>
            indicators.FirstOrDefault(i => i.SeriesId.Equals(id, StringComparison.OrdinalIgnoreCase));

        var unrate = Get("UNRATE");
        var payems = Get("PAYEMS");
        var cpi = Get("CPIAUCSL");
        var ppi = Get("PPIACO");
        var gdp = Get("GDPC1");
        var indpro = Get("INDPRO");
        var fedfunds = Get("FEDFUNDS");
        var gs10 = Get("GS10");
        var sp500 = Get("SP500");
        var recession = Get("RECPROUSM156N");
        var mortgage = Get("MORTGAGE30US");

        // Pattern 1: Yield Curve Inversion + Rising Unemployment
        if (fedfunds?.CurrentValue is not null && gs10?.CurrentValue is not null)
        {
            var spread = gs10.CurrentValue.Value - fedfunds.CurrentValue.Value;
            var isInverted = spread < 0;
            var unemploymentRising = unrate?.TrendDirection == "up";

            if (isInverted && unemploymentRising)
            {
                patterns.Add(new CorrelationPattern(
                    "yield-inversion-unemployment",
                    "Yield Curve Inversion + Rising Unemployment",
                    $"10-year Treasury yield is {Math.Abs(spread):F2}% below Fed Funds rate while unemployment is trending up",
                    ["FEDFUNDS", "GS10", "UNRATE"], 85, "critical", "negative",
                    "Historically strong recession indicator - this pattern has preceded 7 of the last 8 recessions",
                    "When the yield curve inverts and unemployment begins rising, recession typically follows within 6-18 months"));
            }
            else if (isInverted)
            {
                patterns.Add(new CorrelationPattern(
                    "yield-inversion",
                    "Yield Curve Inversion Detected",
                    $"10-year Treasury yield is {Math.Abs(spread):F2}% below Fed Funds rate",
                    ["FEDFUNDS", "GS10"], 70, "high", "negative",
                    "Potential recession warning - markets expect Fed to cut rates in the future",
                    "Yield curve inversions have preceded recessions, though timing varies (6-24 months)"));
            }
        }

        // Pattern 2: Soft Landing Scenario
        if (fedfunds is not null && cpi is not null)
        {
            var fedFalling = fedfunds.TrendDirection == "down";
            var cpiStableOrFalling = cpi.TrendDirection is "down" or "stable";

            if (fedFalling && cpiStableOrFalling)
            {
                patterns.Add(new CorrelationPattern(
                    "soft-landing",
                    "Potential Soft Landing Scenario",
                    "Fed cutting rates while inflation remains controlled",
                    ["FEDFUNDS", "CPIAUCSL"], 65, "low", "positive",
                    "Fed may be successfully reducing inflation without triggering recession",
                    "Soft landings are rare but possible - last achieved in mid-1990s"));
            }
        }

        // Pattern 3: Industrial Production & Employment Both Declining
        if (indpro is not null && payems is not null)
        {
            if (indpro.TrendDirection == "down" && payems.TrendDirection == "down")
            {
                patterns.Add(new CorrelationPattern(
                    "production-employment-decline",
                    "Industrial Production & Employment Both Declining",
                    "Manufacturing output and job growth both trending negative",
                    ["INDPRO", "PAYEMS"], 80, "high", "negative",
                    "Early recession signal - businesses cutting production and workforce",
                    "Simultaneous declines typically indicate economic contraction is underway"));
            }
        }

        // Pattern 4: Inflation Pressure + Fed Tightening
        if (cpi is not null && fedfunds is not null)
        {
            var inflationRising = cpi.TrendDirection == "up" || (ppi?.TrendDirection == "up");
            var ratesRising = fedfunds.TrendDirection == "up";

            if (inflationRising && ratesRising)
            {
                patterns.Add(new CorrelationPattern(
                    "inflation-rate-spiral",
                    "Inflation Pressure + Fed Tightening",
                    "Inflation trending up while Fed raises rates to combat it",
                    ["CPIAUCSL", "PPIACO", "FEDFUNDS"], 75, "high", "negative",
                    "Fed fighting inflation - expect higher borrowing costs and potential economic slowdown",
                    "Aggressive rate hikes to combat inflation often lead to recessions (1980-82, 2001)"));
            }
        }

        // Pattern 5: Goldilocks Economy
        if (gdp is not null && unrate is not null && cpi is not null)
        {
            var gdpStrong = gdp.TrendDirection == "up" && gdp.ChangePercent > 1;
            var unemploymentLow = unrate.CurrentValue.HasValue && unrate.CurrentValue.Value < 4.5;
            var inflationModerate = cpi.ChangePercent is < 3 and > 1.5;

            if (gdpStrong && unemploymentLow && inflationModerate)
            {
                patterns.Add(new CorrelationPattern(
                    "goldilocks-economy",
                    "\"Goldilocks\" Economic Conditions",
                    "Strong growth, low unemployment, and controlled inflation",
                    ["GDPC1", "UNRATE", "CPIAUCSL"], 70, "low", "positive",
                    "Ideal economic conditions - not too hot, not too cold",
                    "Sustainable when productivity growth supports wage gains without triggering inflation"));
            }
        }

        // Pattern 6: Housing Market Stress
        if (mortgage?.CurrentValue is not null && gdp is not null)
        {
            var mortgageHigh = mortgage.CurrentValue.Value > 6.5;
            var economySlowing = gdp.TrendDirection is "down" or "stable";

            if (mortgageHigh && economySlowing)
            {
                patterns.Add(new CorrelationPattern(
                    "housing-stress",
                    "Housing Market Stress",
                    $"Mortgage rates at {mortgage.CurrentValue.Value:F2}% while economy slowing",
                    ["MORTGAGE30US", "GDPC1"], 65, "medium", "negative",
                    "Housing market cooling - affordability challenges and reduced activity",
                    "High mortgage rates typically slow housing market and can impact consumer spending"));
            }
        }

        // Pattern 7: Market-Economy Disconnect
        if (sp500 is not null && recession?.CurrentValue is not null)
        {
            var marketUp = sp500.TrendDirection == "up" && sp500.ChangePercent > 5;
            var recessionSignal = recession.CurrentValue.Value > 30; // > 30%

            if (marketUp && recessionSignal)
            {
                patterns.Add(new CorrelationPattern(
                    "market-disconnect",
                    "Market-Economy Disconnect",
                    "Stock market rallying despite elevated recession risk",
                    ["SP500", "RECPROUSM156N"], 60, "medium", "neutral",
                    "Markets may be pricing in future recovery or Fed pivot - increased volatility likely",
                    "Markets often bottom before recessions end, anticipating recovery"));
            }
        }

        // Pattern 8: Rate Cut Market Rally
        if (fedfunds is not null && sp500 is not null)
        {
            if (fedfunds.TrendDirection == "down" && sp500.TrendDirection == "up")
            {
                patterns.Add(new CorrelationPattern(
                    "rate-cut-rally",
                    "Rate Cut Market Rally",
                    "Declining interest rates supporting stock market gains",
                    ["FEDFUNDS", "SP500"], 75, "low", "positive",
                    "Lower rates boost valuations and reduce borrowing costs - positive for equities",
                    "Rate cut cycles often support market rallies, though timing matters"));
            }
        }

        // Pattern 9: Mixed Economic Signals
        if (payems is not null && indpro is not null)
        {
            if (payems.TrendDirection == "up" && indpro.TrendDirection == "down")
            {
                patterns.Add(new CorrelationPattern(
                    "mixed-signals",
                    "Mixed Economic Signals",
                    "Employment strong but industrial production weak",
                    ["PAYEMS", "INDPRO"], 55, "medium", "neutral",
                    "Economy transitioning - could indicate shift from manufacturing to services",
                    "Diverging indicators suggest economic inflection point - monitor closely"));
            }
        }

        // Determine overall risk
        var criticalCount = patterns.Count(p => p.Severity == "critical");
        var highCount = patterns.Count(p => p.Severity == "high");
        var negativeCount = patterns.Count(p => p.Type == "negative");

        string overallRisk;
        if (criticalCount > 0 || (highCount > 1 && negativeCount > 2))
            overallRisk = "critical";
        else if (highCount > 0 || negativeCount > 1)
            overallRisk = "high";
        else if (patterns.Count > 0 && negativeCount > 0)
            overallRisk = "medium";
        else
            overallRisk = "low";

        var summary = GenerateSummary(patterns, overallRisk);
        return new CorrelationAnalysis(patterns.AsReadOnly(), summary, overallRisk);
    }

    private static string GenerateSummary(List<CorrelationPattern> patterns, string risk)
    {
        if (patterns.Count == 0)
            return "No significant correlation patterns detected at this time. Economic indicators are showing independent trends.";

        var positiveCount = patterns.Count(p => p.Type == "positive");
        var negativeCount = patterns.Count(p => p.Type == "negative");

        var summary = $"Detected {patterns.Count} significant correlation pattern{(patterns.Count > 1 ? "s" : "")}. ";

        if (negativeCount > positiveCount)
            summary += $"Caution advised: {negativeCount} concerning signal{(negativeCount > 1 ? "s" : "")} detected. ";
        else if (positiveCount > negativeCount)
            summary += $"Positive outlook: {positiveCount} favorable signal{(positiveCount > 1 ? "s" : "")} identified. ";
        else
            summary += "Mixed signals present - ";

        summary += risk switch
        {
            "critical" => "Overall economic risk level is CRITICAL. Close monitoring and defensive positioning recommended.",
            "high" => "Overall economic risk level is HIGH. Exercise caution and review your financial positions.",
            "medium" => "Overall economic risk level is MODERATE. Stay informed and maintain balanced approach.",
            _ => "Overall economic risk level is LOW. Conditions appear relatively stable."
        };

        return summary;
    }
}
