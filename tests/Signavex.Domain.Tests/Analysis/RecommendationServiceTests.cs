using Signavex.Domain.Analysis;
using Signavex.Domain.Models.Economic;

namespace Signavex.Domain.Tests.Analysis;

public class RecommendationServiceTests
{
    private readonly RecommendationService _sut = new();
    private readonly EconomicAnalysisService _analysis = new();
    private readonly CorrelationAnalysisService _corrService = new();

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
    public void GetRecommendations_HighRecessionProb_EmergencyFundRecommended()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("RECPROUSM156N", 20, 50, EconomicCategory.Market),
        };

        var correlations = _corrService.Analyze(indicators);
        var plan = _sut.GetRecommendations(UserProfile.General, indicators, correlations);

        Assert.Contains(plan.Recommendations, r => r.Category == "emergency-fund");
    }

    [Fact]
    public void GetRecommendations_RisingUnemployment_EmploymentSecurityRecommended()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("UNRATE", 3.5, 5.0, EconomicCategory.Employment),
        };

        var correlations = _corrService.Analyze(indicators);
        var plan = _sut.GetRecommendations(UserProfile.JobSeeker, indicators, correlations);

        Assert.Contains(plan.Recommendations, r => r.Id == "employment-security");
    }

    [Fact]
    public void GetRecommendations_StrongJobMarket_OpportunityRecommended()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("PAYEMS", 150000, 158000, EconomicCategory.Employment), // rising
            MakeInterp("UNRATE", 4.0, 3.5, EconomicCategory.Employment),       // 3.5 < 4
        };

        var correlations = _corrService.Analyze(indicators);
        var plan = _sut.GetRecommendations(UserProfile.JobSeeker, indicators, correlations);

        Assert.Contains(plan.Recommendations, r => r.Id == "job-opportunity");
    }

    [Fact]
    public void GetRecommendations_HighMortgageRates_RealEstateWarning()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("MORTGAGE30US", 6.0, 7.5, EconomicCategory.Housing),
        };

        var correlations = _corrService.Analyze(indicators);
        var plan = _sut.GetRecommendations(UserProfile.Renter, indicators, correlations);

        Assert.Contains(plan.Recommendations, r => r.Id == "mortgage-high");
    }

    [Fact]
    public void GetRecommendations_FiltersByProfile_BusinessOwnerGetsBusinessRecs()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("RECPROUSM156N", 20, 40, EconomicCategory.Market),
        };

        var correlations = _corrService.Analyze(indicators);
        var plan = _sut.GetRecommendations(UserProfile.BusinessOwner, indicators, correlations);

        Assert.Contains(plan.Recommendations, r => r.Id == "business-cash-flow");
    }

    [Fact]
    public void GetRecommendations_CriticalRisk_DefensivePositionRecommended()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("FEDFUNDS", 5.0, 5.25, EconomicCategory.InterestRates),
            MakeInterp("GS10", 4.0, 3.8, EconomicCategory.InterestRates),
            MakeInterp("UNRATE", 3.5, 5.0, EconomicCategory.Employment),
            MakeInterp("INDPRO", 110, 100, EconomicCategory.Growth),
            MakeInterp("PAYEMS", 155000, 148000, EconomicCategory.Employment),
            MakeInterp("CPIAUCSL", 290, 305, EconomicCategory.Inflation),
        };

        var correlations = _corrService.Analyze(indicators);
        var plan = _sut.GetRecommendations(UserProfile.ConservativeInvestor, indicators, correlations);

        Assert.Contains(plan.Recommendations, r => r.Id == "defensive-position");
    }

    [Fact]
    public void GetRecommendations_SortedByPriority()
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("RECPROUSM156N", 20, 55, EconomicCategory.Market), // high recession prob
            MakeInterp("UNRATE", 3.5, 4.5, EconomicCategory.Employment),  // rising
            MakeInterp("PAYEMS", 155000, 152000, EconomicCategory.Employment), // falling
        };

        var correlations = _corrService.Analyze(indicators);
        var plan = _sut.GetRecommendations(UserProfile.General, indicators, correlations);

        // Verify priority ordering: critical < high < medium < low
        var priorityOrder = new Dictionary<string, int>
        {
            ["critical"] = 0, ["high"] = 1, ["medium"] = 2, ["low"] = 3
        };

        for (int i = 1; i < plan.Recommendations.Count; i++)
        {
            var prevPriority = priorityOrder.GetValueOrDefault(plan.Recommendations[i - 1].Priority, 99);
            var currPriority = priorityOrder.GetValueOrDefault(plan.Recommendations[i].Priority, 99);
            Assert.True(prevPriority <= currPriority,
                $"Recommendations not sorted by priority: {plan.Recommendations[i - 1].Priority} before {plan.Recommendations[i].Priority}");
        }
    }

    [Theory]
    [InlineData(UserProfile.General)]
    [InlineData(UserProfile.ConservativeInvestor)]
    [InlineData(UserProfile.AggressiveInvestor)]
    [InlineData(UserProfile.Homeowner)]
    [InlineData(UserProfile.Renter)]
    [InlineData(UserProfile.JobSeeker)]
    [InlineData(UserProfile.BusinessOwner)]
    public void GetRecommendations_AllProfiles_ReturnValidPlan(UserProfile profile)
    {
        var indicators = new List<IndicatorInterpretation>
        {
            MakeInterp("UNRATE", 4.0, 3.8, EconomicCategory.Employment),
            MakeInterp("FEDFUNDS", 5.0, 4.5, EconomicCategory.InterestRates),
        };

        var correlations = _corrService.Analyze(indicators);
        var plan = _sut.GetRecommendations(profile, indicators, correlations);

        Assert.Equal(profile, plan.Profile);
        Assert.NotNull(plan.Summary);
        Assert.NotNull(plan.EconomicOutlook);
    }
}
