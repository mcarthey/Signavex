namespace Signavex.Domain.Analysis;

public class RecommendationService
{
    public ActionPlan GetRecommendations(
        UserProfile profile,
        IReadOnlyList<IndicatorInterpretation> indicators,
        CorrelationAnalysis correlations)
    {
        var all = new List<ActionRecommendation>();

        IndicatorInterpretation? Get(string id) =>
            indicators.FirstOrDefault(i => i.SeriesId.Equals(id, StringComparison.OrdinalIgnoreCase));

        var unrate = Get("UNRATE");
        var cpi = Get("CPIAUCSL");
        var fedfunds = Get("FEDFUNDS");
        var sp500 = Get("SP500");
        var mortgage = Get("MORTGAGE30US");
        var recession = Get("RECPROUSM156N");
        var payems = Get("PAYEMS");

        var recessionProb = recession?.CurrentValue ?? 0;
        var risk = correlations.OverallRisk;

        // === EMERGENCY FUND ===
        if (recessionProb > 40 || risk is "critical" or "high")
        {
            all.Add(new ActionRecommendation(
                "emergency-fund-boost", "emergency-fund",
                "Increase Emergency Fund",
                "Build emergency savings to 6-12 months of expenses",
                risk == "critical" ? "critical" : "high", "immediate",
                $"With recession probability at {recessionProb:F0}% and {risk} risk level, having robust emergency savings is crucial.",
                ["RECPROUSM156N", "UNRATE"],
                [UserProfile.General, UserProfile.ConservativeInvestor, UserProfile.Homeowner, UserProfile.Renter, UserProfile.JobSeeker]));
        }
        else if (recessionProb > 20)
        {
            all.Add(new ActionRecommendation(
                "emergency-fund-maintain", "emergency-fund",
                "Maintain 3-6 Month Emergency Fund",
                "Ensure you have adequate liquid savings",
                "medium", "short-term",
                $"Economic uncertainty is moderate ({recessionProb:F0}% recession risk). A standard emergency fund provides good protection.",
                ["RECPROUSM156N"],
                [UserProfile.General, UserProfile.ConservativeInvestor, UserProfile.AggressiveInvestor, UserProfile.Homeowner, UserProfile.Renter]));
        }

        // === INVESTMENT ===
        if (sp500 is not null && Math.Abs(sp500.ChangePercent) > 15)
        {
            all.Add(new ActionRecommendation(
                "market-volatility", "investment",
                "High Market Volatility Detected",
                "Consider dollar-cost averaging instead of lump-sum investing",
                "high", "immediate",
                $"S&P 500 has moved {sp500.ChangePercent:F1}% - significant volatility. DCA reduces timing risk.",
                ["SP500"],
                [UserProfile.ConservativeInvestor, UserProfile.AggressiveInvestor, UserProfile.General]));
        }

        if (sp500 is not null && sp500.ChangePercent < -10 && risk != "critical")
        {
            all.Add(new ActionRecommendation(
                "buying-opportunity", "investment",
                "Potential Buying Opportunity",
                $"S&P 500 down {Math.Abs(sp500.ChangePercent):F1}% - consider quality stocks at discount",
                "medium", "short-term",
                "Market corrections create opportunities for long-term investors. Focus on quality companies with strong fundamentals.",
                ["SP500"],
                [UserProfile.AggressiveInvestor, UserProfile.ConservativeInvestor]));
        }

        if (fedfunds is not null && fedfunds.TrendDirection == "up" && fedfunds.CurrentValue > 4)
        {
            all.Add(new ActionRecommendation(
                "bond-opportunity", "investment",
                "Higher Bond Yields Available",
                $"Fed Funds at {fedfunds.CurrentValue:F2}% - bonds more attractive",
                "medium", "short-term",
                "Higher interest rates make fixed income investments more attractive. Consider laddering bonds or CDs.",
                ["FEDFUNDS"],
                [UserProfile.ConservativeInvestor, UserProfile.General]));
        }

        if (risk == "critical")
        {
            all.Add(new ActionRecommendation(
                "defensive-position", "investment",
                "Consider Defensive Positioning",
                "Shift toward defensive sectors, cash, and high-quality bonds",
                "critical", "immediate",
                "Multiple recession signals detected. Defensive assets help preserve capital during downturns.",
                ["RECPROUSM156N", "SP500"],
                [UserProfile.ConservativeInvestor, UserProfile.AggressiveInvestor, UserProfile.General]));
        }

        // === REAL ESTATE ===
        if (mortgage?.CurrentValue > 6.5)
        {
            all.Add(new ActionRecommendation(
                "mortgage-high", "real-estate",
                "Mortgage Rates Elevated",
                $"30-year rates at {mortgage.CurrentValue:F2}% - consider waiting or adjustable rate",
                "high", "medium-term",
                "High mortgage rates significantly impact affordability. Consider ARM or wait for potential rate decreases.",
                ["MORTGAGE30US"],
                [UserProfile.Renter, UserProfile.Homeowner]));
        }

        if (mortgage is not null && mortgage.TrendDirection == "down" && mortgage.CurrentValue < 6)
        {
            all.Add(new ActionRecommendation(
                "mortgage-refinance", "real-estate",
                "Refinancing Opportunity",
                $"Mortgage rates trending down to {mortgage.CurrentValue:F2}%",
                "medium", "short-term",
                "Declining rates create refinancing opportunities. Check if you can lower your rate by 0.5%+ to justify closing costs.",
                ["MORTGAGE30US"],
                [UserProfile.Homeowner]));
        }

        if (recessionProb > 40 && mortgage?.CurrentValue > 6)
        {
            all.Add(new ActionRecommendation(
                "housing-wait", "real-estate",
                "Consider Delaying Home Purchase",
                "High rates + recession risk may lead to price corrections",
                "medium", "medium-term",
                $"Recession probability at {recessionProb:F0}% combined with {mortgage.CurrentValue:F2}% mortgage rates suggests potential for both price and rate decreases ahead.",
                ["MORTGAGE30US", "RECPROUSM156N"],
                [UserProfile.Renter]));
        }

        // === EMPLOYMENT ===
        if (unrate?.TrendDirection == "up")
        {
            all.Add(new ActionRecommendation(
                "employment-security", "employment",
                "Strengthen Job Security",
                "Update skills, network, and maintain strong work performance",
                "high", "immediate",
                $"Unemployment trending up to {unrate.CurrentValue:F1}%. Proactively strengthen your position.",
                ["UNRATE"],
                [UserProfile.General, UserProfile.JobSeeker]));
        }

        if (payems?.TrendDirection == "down")
        {
            all.Add(new ActionRecommendation(
                "job-market-caution", "employment",
                "Job Market Cooling",
                "Hiring slowing - be cautious about job changes",
                "medium", "immediate",
                "Payroll growth declining suggests fewer job opportunities. Focus on job security.",
                ["PAYEMS"],
                [UserProfile.General, UserProfile.JobSeeker]));
        }

        if (payems?.TrendDirection == "up" && unrate?.CurrentValue < 4)
        {
            all.Add(new ActionRecommendation(
                "job-opportunity", "employment",
                "Strong Job Market",
                "Good time for career advancement or job change",
                "low", "short-term",
                $"Low unemployment ({unrate.CurrentValue:F1}%) and growing payrolls indicate strong labor demand.",
                ["UNRATE", "PAYEMS"],
                [UserProfile.General, UserProfile.JobSeeker]));
        }

        // === SPENDING / DEBT ===
        if (cpi is not null && cpi.ChangePercent > 3)
        {
            all.Add(new ActionRecommendation(
                "inflation-spending", "spending",
                "Combat Inflation Impact",
                "Review budget, focus on necessities, consider inflation-protected assets",
                "high", "immediate",
                $"Inflation at {cpi.ChangePercent:F1}% erodes purchasing power. Review discretionary spending, consider I-Bonds or TIPS.",
                ["CPIAUCSL"],
                [UserProfile.General, UserProfile.ConservativeInvestor, UserProfile.Renter, UserProfile.Homeowner]));
        }

        if (fedfunds?.TrendDirection == "up")
        {
            all.Add(new ActionRecommendation(
                "debt-paydown", "debt",
                "Pay Down Variable-Rate Debt",
                "Focus on credit cards and variable loans as rates rise",
                "high", "immediate",
                $"Fed raising rates to {fedfunds.CurrentValue:F2}% increases cost of variable-rate debt.",
                ["FEDFUNDS"],
                [UserProfile.General, UserProfile.Homeowner, UserProfile.Renter, UserProfile.BusinessOwner]));
        }

        if (recessionProb > 50)
        {
            all.Add(new ActionRecommendation(
                "recession-preparation", "spending",
                "Prepare for Economic Downturn",
                "Reduce discretionary spending, delay major purchases",
                "critical", "immediate",
                $"Recession probability at {recessionProb:F0}%. Conserve cash, defer non-essential purchases.",
                ["RECPROUSM156N"],
                [UserProfile.General, UserProfile.Homeowner, UserProfile.Renter, UserProfile.BusinessOwner]));
        }

        // === BUSINESS OWNER ===
        if (recessionProb > 30 || risk is "high" or "critical")
        {
            all.Add(new ActionRecommendation(
                "business-cash-flow", "spending",
                "Strengthen Business Cash Position",
                "Build cash reserves, review expenses, secure credit lines",
                risk == "critical" ? "critical" : "high", "immediate",
                "Economic uncertainty ahead. Increase cash reserves, defer non-critical investments.",
                ["RECPROUSM156N", "FEDFUNDS"],
                [UserProfile.BusinessOwner]));
        }

        // Filter by profile and sort by priority
        var priorityOrder = new Dictionary<string, int>
        {
            ["critical"] = 0, ["high"] = 1, ["medium"] = 2, ["low"] = 3
        };

        var filtered = all
            .Where(r => r.Profiles.Contains(profile) || r.Profiles.Contains(UserProfile.General))
            .OrderBy(r => priorityOrder.GetValueOrDefault(r.Priority, 99))
            .ToList()
            .AsReadOnly();

        var summary = GenerateSummary(filtered, risk);
        var outlook = GenerateOutlook(risk, recessionProb);

        return new ActionPlan(profile, filtered, summary, outlook);
    }

    private static string GenerateSummary(IReadOnlyList<ActionRecommendation> recommendations, string risk)
    {
        var criticalCount = recommendations.Count(r => r.Priority == "critical");
        var highCount = recommendations.Count(r => r.Priority == "high");

        if (criticalCount > 0)
            return $"{criticalCount} critical action{(criticalCount > 1 ? "s" : "")} recommended. Immediate attention required to protect your financial position.";
        if (highCount > 0)
            return $"{highCount} high-priority action{(highCount > 1 ? "s" : "")} suggested based on current economic conditions.";
        if (recommendations.Count > 0)
            return $"{recommendations.Count} recommendation{(recommendations.Count > 1 ? "s" : "")} to optimize your financial position.";
        return "No urgent actions required. Continue monitoring economic conditions.";
    }

    private static string GenerateOutlook(string risk, double recessionProb)
    {
        return risk switch
        {
            "critical" => $"Economic outlook is concerning with {recessionProb:F0}% recession probability. Multiple warning signals detected. Focus on capital preservation and risk management.",
            "high" => $"Economic conditions show elevated risk ({recessionProb:F0}% recession probability). Exercise caution with major financial decisions.",
            "medium" => "Economic outlook is mixed with moderate uncertainty. Stay informed and maintain a balanced, diversified approach.",
            _ => $"Economic conditions appear relatively stable with {recessionProb:F0}% recession probability. Good time for planned financial moves."
        };
    }
}
