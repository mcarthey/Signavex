using Signavex.Domain.Models.Economic;

namespace Signavex.Domain.Analysis;

public class EconomicAnalysisService
{
    public IndicatorInterpretation AnalyzeIndicator(
        EconomicSeries series,
        IReadOnlyList<EconomicObservation> observations)
    {
        var metadata = IndicatorMetadata.Get(series.SeriesId);
        var currentValue = observations.Count > 0 ? observations[^1].Value : (double?)null;
        var previousValue = observations.Count > 1 ? observations[^2].Value : (double?)null;

        double changePercent = 0;
        if (currentValue.HasValue && previousValue.HasValue && previousValue.Value != 0)
            changePercent = ((currentValue.Value - previousValue.Value) / Math.Abs(previousValue.Value)) * 100;

        if (metadata is null)
        {
            return new IndicatorInterpretation(
                series.SeriesId, series.Name, series.Description, series.Category,
                currentValue, previousValue, CalculateTrend(changePercent), changePercent,
                "neutral", "mild", "No assessment available", "",
                null, FormatValue(currentValue, null), FormatChange(changePercent));
        }

        var trend = CalculateTrend(changePercent);
        var sentiment = CalculateSentiment(trend, metadata.GoodDirection);
        var severity = CalculateSeverity(changePercent);
        var assessment = GenerateAssessment(changePercent, sentiment);
        var implications = GenerateImplications(trend, metadata);
        var benchmark = GenerateBenchmarkComparison(currentValue, metadata);

        return new IndicatorInterpretation(
            series.SeriesId, series.Name, series.Description, metadata.Category,
            currentValue, previousValue, trend, changePercent,
            sentiment, severity, assessment, implications,
            benchmark, FormatValue(currentValue, metadata), FormatChange(changePercent));
    }

    public IReadOnlyList<CategoryHealth> CalculateCategoryHealth(
        IReadOnlyList<IndicatorInterpretation> interpretations)
    {
        var groups = interpretations
            .GroupBy(i => i.Category)
            .Select(g =>
            {
                var indicators = g.ToList();
                var score = CalculateCategoryScore(indicators);
                var trend = CalculateCategoryTrend(indicators);
                var categoryName = GetCategoryName(g.Key);
                return new CategoryHealth(categoryName, g.Key, score, trend, indicators.AsReadOnly());
            })
            .OrderBy(c => GetCategoryOrder(c.Category))
            .ToList();

        return groups.AsReadOnly();
    }

    public EconomicHealthSummary CalculateEconomicHealth(
        IReadOnlyList<IndicatorInterpretation> interpretations)
    {
        var categories = CalculateCategoryHealth(interpretations);
        var overallScore = CalculateOverallHealth(categories);
        var label = GetHealthLabel(overallScore);
        var insights = GenerateKeyInsights(interpretations);

        return new EconomicHealthSummary(overallScore, label, categories, insights, DateTime.UtcNow);
    }

    public static string CalculateTrend(double changePercent)
    {
        if (Math.Abs(changePercent) < 0.5) return "stable";
        return changePercent > 0 ? "up" : "down";
    }

    public static string CalculateSentiment(string trend, string goodDirection)
    {
        if (trend == "stable") return "neutral";
        if (trend == goodDirection) return "positive";
        return "negative";
    }

    public static string CalculateSeverity(double changePercent)
    {
        var abs = Math.Abs(changePercent);
        if (abs > 5) return "strong";
        if (abs > 2) return "moderate";
        return "mild";
    }

    private static string GenerateAssessment(double changePercent, string sentiment)
    {
        var abs = Math.Abs(changePercent);
        var direction = changePercent > 0 ? "rising" : "falling";
        var isGood = sentiment == "positive";

        if (abs < 0.5)
            return "Stable with minimal change";
        if (abs < 2)
            return $"{(isGood ? "Favorable" : "Unfavorable")} - {direction} moderately";
        if (abs < 5)
            return $"{(isGood ? "Positive" : "Concerning")} - {direction} significantly";
        return $"{(isGood ? "Strong improvement" : "Sharp decline")} - {direction} rapidly";
    }

    private static string GenerateImplications(string trend, IndicatorMetadataEntry metadata)
    {
        if (trend == "stable")
            return "Maintaining current levels suggests stability in this economic area";
        return trend == "up" ? metadata.ImplicationsRising : metadata.ImplicationsFalling;
    }

    private static string? GenerateBenchmarkComparison(double? currentValue, IndicatorMetadataEntry metadata)
    {
        if (!currentValue.HasValue) return null;

        var comparisons = new List<string>();
        var value = currentValue.Value;

        if (metadata.PreCovidAvg.HasValue)
        {
            var diff = value - metadata.PreCovidAvg.Value;
            var pct = Math.Abs(diff / metadata.PreCovidAvg.Value * 100);
            var dir = diff > 0 ? "above" : "below";
            comparisons.Add($"{pct:F1}% {dir} pre-COVID avg ({FormatValue(metadata.PreCovidAvg, metadata)})");
        }

        if (metadata.HistoricalAvg.HasValue)
        {
            var diff = value - metadata.HistoricalAvg.Value;
            var pct = Math.Abs(diff / metadata.HistoricalAvg.Value * 100);
            var dir = diff > 0 ? "above" : "below";
            comparisons.Add($"{pct:F1}% {dir} long-term avg ({FormatValue(metadata.HistoricalAvg, metadata)})");
        }

        if (metadata.RecessionLevel.HasValue && metadata.ExpansionLevel.HasValue)
        {
            if (value <= metadata.RecessionLevel.Value)
                comparisons.Add("Near recession levels");
            else if (value >= metadata.ExpansionLevel.Value)
                comparisons.Add("At expansion levels");
        }

        return comparisons.Count > 0 ? string.Join(" | ", comparisons) : null;
    }

    private static string FormatValue(double? value, IndicatorMetadataEntry? metadata)
    {
        if (!value.HasValue) return "N/A";
        var v = value.Value;
        var decimals = metadata?.Decimals ?? 2;
        var formatted = v.ToString($"F{decimals}");
        if (metadata?.IsCurrency == true) formatted = "$" + formatted;
        if (metadata?.Suffix is not null) formatted += metadata.Suffix;
        return formatted;
    }

    private static string FormatChange(double changePercent)
    {
        var sign = changePercent > 0 ? "+" : "";
        return $"{sign}{changePercent:F2}%";
    }

    private static double CalculateCategoryScore(List<IndicatorInterpretation> indicators)
    {
        if (indicators.Count == 0) return 50;
        var scores = indicators.Select(i => i.Sentiment switch
        {
            "positive" => 100.0,
            "neutral" => 50.0,
            _ => 0.0
        });
        return scores.Average();
    }

    private static string CalculateCategoryTrend(List<IndicatorInterpretation> indicators)
    {
        var positive = indicators.Count(i => i.Sentiment == "positive");
        var negative = indicators.Count(i => i.Sentiment == "negative");
        if (positive > negative) return "up";
        if (negative > positive) return "down";
        return "stable";
    }

    private static int CalculateOverallHealth(IReadOnlyList<CategoryHealth> categories)
    {
        if (categories.Count == 0) return 50;

        var weights = new Dictionary<EconomicCategory, double>
        {
            [EconomicCategory.Employment] = 1.5,
            [EconomicCategory.Inflation] = 1.5,
            [EconomicCategory.Growth] = 1.2,
            [EconomicCategory.InterestRates] = 0.8,
            [EconomicCategory.Market] = 0.8,
            [EconomicCategory.Housing] = 0.8,
            [EconomicCategory.Consumer] = 1.0
        };

        double totalScore = 0;
        double totalWeight = 0;

        foreach (var cat in categories)
        {
            var weight = weights.GetValueOrDefault(cat.Category, 1.0);
            totalScore += cat.Score * weight;
            totalWeight += weight;
        }

        return (int)Math.Round(totalScore / totalWeight);
    }

    private static string GetHealthLabel(int score) => score switch
    {
        >= 80 => "Excellent",
        >= 65 => "Good",
        >= 50 => "Fair",
        >= 35 => "Weak",
        _ => "Poor"
    };

    private static IReadOnlyList<KeyInsight> GenerateKeyInsights(
        IReadOnlyList<IndicatorInterpretation> indicators)
    {
        var insights = new List<KeyInsight>();

        // Strong positive
        var strongPositive = indicators.FirstOrDefault(
            i => i.Sentiment == "positive" && i.Severity == "strong");
        if (strongPositive is not null)
        {
            insights.Add(new KeyInsight("positive",
                $"Strong: {strongPositive.Description}",
                $"{strongPositive.Description} {(strongPositive.TrendDirection == "up" ? "increased" : "decreased")} by {strongPositive.FormattedChange}",
                [strongPositive.SeriesId]));
        }

        // Strong negative
        var strongNegative = indicators.FirstOrDefault(
            i => i.Sentiment == "negative" && i.Severity == "strong");
        if (strongNegative is not null)
        {
            insights.Add(new KeyInsight("negative",
                $"Watch: {strongNegative.Description}",
                $"{strongNegative.Description} {(strongNegative.TrendDirection == "up" ? "rose" : "fell")} by {strongNegative.FormattedChange}",
                [strongNegative.SeriesId]));
        }

        // Inflation moderating
        var inflationIndicators = indicators
            .Where(i => i.Category == EconomicCategory.Inflation).ToList();
        if (inflationIndicators.Count > 0 &&
            inflationIndicators.Count(i => i.TrendDirection == "down") > inflationIndicators.Count / 2)
        {
            insights.Add(new KeyInsight("positive",
                "Inflation Moderating",
                "Price pressures showing signs of easing",
                inflationIndicators.Select(i => i.SeriesId).ToList()));
        }

        // Employment health
        var employmentIndicators = indicators
            .Where(i => i.Category == EconomicCategory.Employment).ToList();
        if (employmentIndicators.Count > 0 &&
            employmentIndicators.All(i => i.Sentiment == "positive"))
        {
            insights.Add(new KeyInsight("positive",
                "Labor Market Strong",
                "Employment indicators remain healthy",
                employmentIndicators.Select(i => i.SeriesId).ToList()));
        }

        return insights.Take(4).ToList().AsReadOnly();
    }

    private static string GetCategoryName(EconomicCategory category) => category switch
    {
        EconomicCategory.Employment => "Employment & Labor",
        EconomicCategory.Inflation => "Inflation & Prices",
        EconomicCategory.Growth => "Economic Growth",
        EconomicCategory.InterestRates => "Markets & Rates",
        EconomicCategory.Market => "Markets",
        EconomicCategory.Housing => "Housing Market",
        EconomicCategory.Consumer => "Consumer Health",
        _ => category.ToString()
    };

    private static int GetCategoryOrder(EconomicCategory category) => category switch
    {
        EconomicCategory.Employment => 0,
        EconomicCategory.Inflation => 1,
        EconomicCategory.Growth => 2,
        EconomicCategory.InterestRates => 3,
        EconomicCategory.Market => 4,
        EconomicCategory.Housing => 5,
        EconomicCategory.Consumer => 6,
        _ => 99
    };
}
