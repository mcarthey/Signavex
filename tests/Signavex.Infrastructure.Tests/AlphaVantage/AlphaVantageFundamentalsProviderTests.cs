using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Signavex.Infrastructure.AlphaVantage;
using Signavex.Infrastructure.AlphaVantage.Dtos;
using Signavex.Infrastructure.Tests.Helpers;

namespace Signavex.Infrastructure.Tests.AlphaVantage;

public class AlphaVantageFundamentalsProviderTests
{
    private const string ValidOverviewJson = """
    {
        "Symbol": "AAPL",
        "PERatio": "28.50",
        "EPS": "6.05",
        "AnalystRatingStrongBuy": "10",
        "AnalystRatingBuy": "15",
        "AnalystRatingHold": "5",
        "AnalystRatingSell": "2",
        "AnalystRatingStrongSell": "1"
    }
    """;

    private const string ValidEarningsJson = """
    {
        "symbol": "AAPL",
        "quarterlyEarnings": [
            {
                "fiscalDateEnding": "2023-09-30",
                "reportedEPS": "2.07",
                "estimatedEPS": "2.10"
            },
            {
                "fiscalDateEnding": "2023-06-30",
                "reportedEPS": "1.26",
                "estimatedEPS": "1.20"
            }
        ]
    }
    """;

    private static AlphaVantageFundamentalsProvider CreateProvider(SequentialMockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.alphavantage.co") };
        return new AlphaVantageFundamentalsProvider(httpClient, TestOptionsFactory.CreateDefault(), NullLogger<AlphaVantageFundamentalsProvider>.Instance);
    }

    [Fact]
    public async Task GetFundamentalsAsync_ValidResponse_ReturnsFundamentals()
    {
        var handler = new SequentialMockHttpMessageHandler(
            (ValidOverviewJson, HttpStatusCode.OK),
            (ValidEarningsJson, HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetFundamentalsAsync("AAPL");

        Assert.Equal("AAPL", result.Ticker);
        Assert.Equal(28.50, result.PeRatio);
        Assert.Equal(6.05, result.EpsPreviousYear);
        Assert.Equal(2.07, result.EpsCurrentQuarter);
        Assert.Equal(2.10, result.EpsEstimateCurrentQuarter);
        Assert.NotNull(result.AnalystRating);
    }

    [Fact]
    public async Task GetFundamentalsAsync_ParsesPeRatio()
    {
        var handler = new SequentialMockHttpMessageHandler(
            ("""{ "PERatio": "25.50" }""", HttpStatusCode.OK),
            ("""{ "quarterlyEarnings": [] }""", HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetFundamentalsAsync("MSFT");

        Assert.Equal(25.50, result.PeRatio);
    }

    [Fact]
    public async Task GetFundamentalsAsync_ParsesAnalystRating_Buy()
    {
        var handler = new SequentialMockHttpMessageHandler(
            ("""
            {
                "AnalystRatingStrongBuy": "5",
                "AnalystRatingBuy": "10",
                "AnalystRatingHold": "3",
                "AnalystRatingSell": "1",
                "AnalystRatingStrongSell": "0"
            }
            """, HttpStatusCode.OK),
            ("""{ "quarterlyEarnings": [] }""", HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetFundamentalsAsync("AAPL");

        Assert.Equal("Buy", result.AnalystRating);
    }

    [Fact]
    public async Task GetFundamentalsAsync_ParsesAnalystRating_Hold()
    {
        var handler = new SequentialMockHttpMessageHandler(
            ("""
            {
                "AnalystRatingStrongBuy": "2",
                "AnalystRatingBuy": "3",
                "AnalystRatingHold": "10",
                "AnalystRatingSell": "2",
                "AnalystRatingStrongSell": "1"
            }
            """, HttpStatusCode.OK),
            ("""{ "quarterlyEarnings": [] }""", HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetFundamentalsAsync("AAPL");

        Assert.Equal("Hold", result.AnalystRating);
    }

    [Fact]
    public async Task GetFundamentalsAsync_ParsesAnalystRating_StrongBuy()
    {
        var handler = new SequentialMockHttpMessageHandler(
            ("""
            {
                "AnalystRatingStrongBuy": "15",
                "AnalystRatingBuy": "5",
                "AnalystRatingHold": "3",
                "AnalystRatingSell": "1",
                "AnalystRatingStrongSell": "0"
            }
            """, HttpStatusCode.OK),
            ("""{ "quarterlyEarnings": [] }""", HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetFundamentalsAsync("AAPL");

        Assert.Equal("Strong Buy", result.AnalystRating);
    }

    [Fact]
    public async Task GetFundamentalsAsync_ParsesEarningsData()
    {
        var handler = new SequentialMockHttpMessageHandler(
            ("""{ "PERatio": "20.0" }""", HttpStatusCode.OK),
            (ValidEarningsJson, HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetFundamentalsAsync("AAPL");

        Assert.Equal(2.07, result.EpsCurrentQuarter);
        Assert.Equal(2.10, result.EpsEstimateCurrentQuarter);
    }

    [Fact]
    public async Task GetFundamentalsAsync_MissingFields_ReturnsNulls()
    {
        var handler = new SequentialMockHttpMessageHandler(
            ("""{ "PERatio": "None", "EPS": "-" }""", HttpStatusCode.OK),
            ("""{ "quarterlyEarnings": [] }""", HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetFundamentalsAsync("XYZ");

        Assert.Equal("XYZ", result.Ticker);
        Assert.Null(result.PeRatio);
        Assert.Null(result.EpsPreviousYear);
        Assert.Null(result.EpsCurrentQuarter);
        Assert.Null(result.EpsEstimateCurrentQuarter);
    }

    [Fact]
    public async Task GetFundamentalsAsync_HttpError_ReturnsNullFundamentals()
    {
        var handler = new SequentialMockHttpMessageHandler(
            ("error", HttpStatusCode.InternalServerError));
        var provider = CreateProvider(handler);

        var result = await provider.GetFundamentalsAsync("AAPL");

        Assert.Equal("AAPL", result.Ticker);
        Assert.Null(result.PeRatio);
        Assert.Null(result.EpsCurrentQuarter);
        Assert.Null(result.AnalystRating);
    }

    [Fact]
    public async Task GetFundamentalsAsync_IndustryPeRatio_AlwaysNull()
    {
        var handler = new SequentialMockHttpMessageHandler(
            (ValidOverviewJson, HttpStatusCode.OK),
            (ValidEarningsJson, HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetFundamentalsAsync("AAPL");

        Assert.Null(result.IndustryPeRatio);
    }

    [Fact]
    public void DeriveAnalystRating_NullOverview_ReturnsNull()
    {
        var result = AlphaVantageFundamentalsProvider.DeriveAnalystRating(null);
        Assert.Null(result);
    }

    [Fact]
    public void DeriveAnalystRating_AllZeros_ReturnsNull()
    {
        var overview = new AlphaVantageOverviewResponse
        {
            AnalystRatingStrongBuy = "0",
            AnalystRatingBuy = "0",
            AnalystRatingHold = "0",
            AnalystRatingSell = "0",
            AnalystRatingStrongSell = "0"
        };

        var result = AlphaVantageFundamentalsProvider.DeriveAnalystRating(overview);
        Assert.Null(result);
    }
}
