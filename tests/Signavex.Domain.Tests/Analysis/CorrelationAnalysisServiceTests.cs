using Signavex.Domain.Analysis;
using Signavex.Domain.Models.Economic;

namespace Signavex.Domain.Tests.Analysis;

public class CorrelationAnalysisServiceTests
{
    private readonly CorrelationAnalysisService _sut = new();
    private readonly EconomicAnalysisService _analysis = new();

    private IndicatorInterpretation MakeInterp(string id, double prev, double curr, EconomicCategory cat)
    {
        var series = new EconomicSeries(id, id, id, "Monthly", "%", "SA", null, true, cat);
        var obs = new List<EconomicObservation>
        {
            new(id, new DateOnly(2024, 1, 1), prev),
            new(id, new DateOnly(2024, 2, 1), curr)
        };
        return _analysis.AnalyzeIndicator(series, obs);
    }

    [Fact]
    public void Analyze_YieldCurveInversion_DetectsPattern()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("FEDFUNDS", 5.0, 5.25, EconomicCategory.InterestRates), // GS10 < FEDFUNDS = inversion
            MakeInterp("GS10", 4.0, 3.8, EconomicCategory.InterestRates),       // 3.8 < 5.25
        };

        var result = _sut.Analyze(indicators);

        Assert.Contains(result.Patterns, p => p.Id == "yield-inversion");
    }

    [Fact]
    public void Analyze_YieldCurveInversionPlusRisingUnemployment_CriticalPattern()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("FEDFUNDS", 5.0, 5.25, EconomicCategory.InterestRates),
            MakeInterp("GS10", 4.0, 3.8, EconomicCategory.InterestRates),
            MakeInterp("UNRATE", 3.5, 4.5, EconomicCategory.Employment), // rising unemployment
        };

        var result = _sut.Analyze(indicators);

        Assert.Contains(result.Patterns, p => p.Id == "yield-inversion-unemployment");
        Assert.Equal("critical", result.Patterns.First(p => p.Id == "yield-inversion-unemployment").Severity);
    }

    [Fact]
    public void Analyze_SoftLanding_DetectsPattern()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("FEDFUNDS", 5.25, 4.5, EconomicCategory.InterestRates), // falling
            MakeInterp("CPIAUCSL", 300, 298, EconomicCategory.Inflation),       // falling = stable/falling
        };

        var result = _sut.Analyze(indicators);

        Assert.Contains(result.Patterns, p => p.Id == "soft-landing");
    }

    [Fact]
    public void Analyze_ProductionEmploymentDecline_DetectsPattern()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("INDPRO", 110, 100, EconomicCategory.Growth),       // falling
            MakeInterp("PAYEMS", 155000, 148000, EconomicCategory.Employment), // falling
        };

        var result = _sut.Analyze(indicators);

        Assert.Contains(result.Patterns, p => p.Id == "production-employment-decline");
    }

    [Fact]
    public void Analyze_RateCutRally_DetectsPattern()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("FEDFUNDS", 5.0, 4.0, EconomicCategory.InterestRates), // falling
            MakeInterp("SP500", 4500, 4800, EconomicCategory.Market),          // rising
        };

        var result = _sut.Analyze(indicators);

        Assert.Contains(result.Patterns, p => p.Id == "rate-cut-rally");
    }

    [Fact]
    public void Analyze_NoPatterns_LowRisk()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("UNRATE", 4.0, 4.0, EconomicCategory.Employment), // stable
        };

        var result = _sut.Analyze(indicators);

        Assert.Equal("low", result.OverallRisk);
    }

    [Fact]
    public void Analyze_MultipleCritical_CriticalRisk()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("FEDFUNDS", 5.0, 5.25, EconomicCategory.InterestRates),
            MakeInterp("GS10", 4.0, 3.8, EconomicCategory.InterestRates),
            MakeInterp("UNRATE", 3.5, 4.5, EconomicCategory.Employment),
            MakeInterp("INDPRO", 110, 100, EconomicCategory.Growth),
            MakeInterp("PAYEMS", 155000, 148000, EconomicCategory.Employment),
            MakeInterp("CPIAUCSL", 290, 305, EconomicCategory.Inflation),
        };

        var result = _sut.Analyze(indicators);

        Assert.True(result.OverallRisk is "critical" or "high");
    }

    [Fact]
    public void Analyze_EmptyIndicators_ReturnsLowRisk()
    {
        var result = _sut.Analyze(Array.Empty<IndicatorInterpretation>());

        Assert.Equal("low", result.OverallRisk);
        Assert.Empty(result.Patterns);
    }
}
