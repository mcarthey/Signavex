using Signavex.Domain.Enums;
using Signavex.Domain.Helpers;
using Signavex.Domain.Models;

namespace Signavex.Engine.Tests;

public class CsvExportHelperTests
{
    private static readonly MarketContext NeutralMarket = new(1.0, "Neutral", Array.Empty<SignalResult>());

    private static StockCandidate CreateCandidate(
        string ticker = "TEST",
        string companyName = "Test Corp",
        double rawScore = 0.75,
        double finalScore = 0.80,
        IEnumerable<SignalResult>? signals = null)
    {
        signals ??= new[]
        {
            new SignalResult("TrendDirection", 0.6, 1.5, "uptrend", true),
            new SignalResult("Volume", 0.4, 1.0, "above average", true)
        };

        return new StockCandidate(
            ticker, companyName, MarketTier.SP500,
            rawScore, finalScore, signals,
            NeutralMarket, DateTime.UtcNow);
    }

    [Fact]
    public void GenerateCsv_EmptyList_ReturnsHeaderOnly()
    {
        var csv = CsvExportHelper.GenerateCsv(Array.Empty<StockCandidate>());

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.StartsWith("Ticker,Company,Tier,RawScore,FinalScore", lines[0]);
    }

    [Fact]
    public void GenerateCsv_SingleCandidate_CorrectColumns()
    {
        var candidate = CreateCandidate(ticker: "AAPL", companyName: "Apple Inc");

        var csv = CsvExportHelper.GenerateCsv(new[] { candidate });

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // header + 1 data row

        var dataLine = lines[1];
        Assert.StartsWith("AAPL,Apple Inc,SP500,", dataLine);
        Assert.Contains("0.7500", dataLine);
        Assert.Contains("0.8000", dataLine);
    }

    [Fact]
    public void GenerateCsv_CompanyNameWithComma_EscapedCorrectly()
    {
        var candidate = CreateCandidate(companyName: "Acme, Inc.");

        var csv = CsvExportHelper.GenerateCsv(new[] { candidate });

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("\"Acme, Inc.\"", lines[1]);
    }

    [Fact]
    public void GenerateCsv_IncludesSignalScores()
    {
        var signals = new[]
        {
            new SignalResult("TrendDirection", 0.65, 1.5, "uptrend", true),
            new SignalResult("Volume", -0.30, 1.0, "below average", true),
            new SignalResult("Unavailable", 0.0, 1.0, "no data", false)
        };
        var candidate = CreateCandidate(signals: signals);

        var csv = CsvExportHelper.GenerateCsv(new[] { candidate });

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        // Should include available signals only
        Assert.Contains("TrendDirection=0.65", lines[1]);
        Assert.Contains("Volume=-0.30", lines[1]);
        Assert.DoesNotContain("Unavailable", lines[1]);
    }
}
