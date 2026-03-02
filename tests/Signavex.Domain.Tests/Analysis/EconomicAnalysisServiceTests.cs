using Signavex.Domain.Analysis;
using Signavex.Domain.Models.Economic;

namespace Signavex.Domain.Tests.Analysis;

public class EconomicAnalysisServiceTests
{
    private readonly EconomicAnalysisService _sut = new();

    private static EconomicSeries MakeSeries(string id, EconomicCategory cat = EconomicCategory.Employment)
        => new(id, id, $"Description of {id}", "Monthly", "Percent", "SA", null, true, cat);

    private static IReadOnlyList<EconomicObservation> MakeObs(string id, params double[] values)
    {
        var obs = new List<EconomicObservation>();
        var date = new DateOnly(2024, 1, 1);
        foreach (var v in values)
        {
            obs.Add(new EconomicObservation(id, date, v));
            date = date.AddMonths(1);
        }
        return obs.AsReadOnly();
    }

    [Fact]
    public void AnalyzeIndicator_RisingValue_DetectsTrendUp()
    {
        var series = MakeSeries("UNRATE");
        var obs = MakeObs("UNRATE", 3.5, 3.6, 3.8, 4.0);

        var result = _sut.AnalyzeIndicator(series, obs);

        Assert.Equal("up", result.TrendDirection);
        Assert.True(result.ChangePercent > 0);
    }

    [Fact]
    public void AnalyzeIndicator_FallingValue_DetectsTrendDown()
    {
        var series = MakeSeries("UNRATE");
        var obs = MakeObs("UNRATE", 5.0, 4.5, 4.0, 3.5);

        var result = _sut.AnalyzeIndicator(series, obs);

        Assert.Equal("down", result.TrendDirection);
        Assert.True(result.ChangePercent < 0);
    }

    [Fact]
    public void AnalyzeIndicator_StableValue_DetectsStable()
    {
        var series = MakeSeries("UNRATE");
        var obs = MakeObs("UNRATE", 4.0, 4.0, 4.0, 4.001);

        var result = _sut.AnalyzeIndicator(series, obs);

        Assert.Equal("stable", result.TrendDirection);
    }

    [Fact]
    public void AnalyzeIndicator_UnrateDown_PositiveSentiment()
    {
        // UNRATE has goodDirection = "down"
        var series = MakeSeries("UNRATE");
        var obs = MakeObs("UNRATE", 5.0, 4.5, 4.0, 3.5);

        var result = _sut.AnalyzeIndicator(series, obs);

        Assert.Equal("positive", result.Sentiment);
    }

    [Fact]
    public void AnalyzeIndicator_UnrateUp_NegativeSentiment()
    {
        var series = MakeSeries("UNRATE");
        var obs = MakeObs("UNRATE", 3.5, 3.8, 4.5, 5.5);

        var result = _sut.AnalyzeIndicator(series, obs);

        Assert.Equal("negative", result.Sentiment);
    }

    [Fact]
    public void AnalyzeIndicator_UnknownSeries_ReturnsNeutral()
    {
        var series = MakeSeries("UNKNOWN_SERIES");
        var obs = MakeObs("UNKNOWN_SERIES", 100, 200);

        var result = _sut.AnalyzeIndicator(series, obs);

        Assert.Equal("neutral", result.Sentiment);
    }

    [Fact]
    public void CalculateCategoryHealth_AllPositive_HighScore()
    {
        var series = MakeSeries("UNRATE");
        var obs1 = MakeObs("UNRATE", 5.0, 4.0); // falling = positive for UNRATE
        var series2 = MakeSeries("PAYEMS");
        var obs2 = MakeObs("PAYEMS", 150000, 155000); // rising = positive for PAYEMS

        var interps = new List<IndicatorInterpretation>
        {
            _sut.AnalyzeIndicator(series, obs1),
            _sut.AnalyzeIndicator(series2, obs2)
        };

        var categories = _sut.CalculateCategoryHealth(interps);

        Assert.Single(categories);
        Assert.Equal(100, categories[0].Score);
    }

    [Fact]
    public void CalculateEconomicHealth_ReturnsValidSummary()
    {
        var interpretations = new List<IndicatorInterpretation>();

        // Create mixed signals
        var unrate = MakeSeries("UNRATE");
        interpretations.Add(_sut.AnalyzeIndicator(unrate, MakeObs("UNRATE", 5.0, 4.0)));

        var cpi = MakeSeries("CPIAUCSL", EconomicCategory.Inflation);
        interpretations.Add(_sut.AnalyzeIndicator(cpi, MakeObs("CPIAUCSL", 290, 300)));

        var gdp = MakeSeries("GDPC1", EconomicCategory.Growth);
        interpretations.Add(_sut.AnalyzeIndicator(gdp, MakeObs("GDPC1", 20000, 20500)));

        var summary = _sut.CalculateEconomicHealth(interpretations);

        Assert.InRange(summary.OverallScore, 0, 100);
        Assert.NotEmpty(summary.ScoreLabel);
        Assert.NotEmpty(summary.CategoryScores);
    }

    [Theory]
    [InlineData(0.0, "stable")]
    [InlineData(0.3, "stable")]
    [InlineData(-0.3, "stable")]
    [InlineData(1.0, "up")]
    [InlineData(-1.0, "down")]
    [InlineData(10.0, "up")]
    [InlineData(-10.0, "down")]
    public void CalculateTrend_ReturnsCorrectDirection(double changePercent, string expected)
    {
        Assert.Equal(expected, EconomicAnalysisService.CalculateTrend(changePercent));
    }

    [Theory]
    [InlineData("up", "up", "positive")]
    [InlineData("down", "down", "positive")]
    [InlineData("up", "down", "negative")]
    [InlineData("down", "up", "negative")]
    [InlineData("stable", "up", "neutral")]
    [InlineData("stable", "down", "neutral")]
    public void CalculateSentiment_ReturnsCorrectResult(string trend, string goodDir, string expected)
    {
        Assert.Equal(expected, EconomicAnalysisService.CalculateSentiment(trend, goodDir));
    }

    [Theory]
    [InlineData(0.5, "mild")]
    [InlineData(2.5, "moderate")]
    [InlineData(6.0, "strong")]
    public void CalculateSeverity_ReturnsCorrectLevel(double changePercent, string expected)
    {
        Assert.Equal(expected, EconomicAnalysisService.CalculateSeverity(changePercent));
    }
}
