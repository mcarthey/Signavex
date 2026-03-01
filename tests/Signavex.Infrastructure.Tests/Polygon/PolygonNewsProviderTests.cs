using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Signavex.Infrastructure.Polygon;
using Signavex.Infrastructure.Tests.Helpers;

namespace Signavex.Infrastructure.Tests.Polygon;

public class PolygonNewsProviderTests
{
    private static PolygonNewsProvider CreateProvider(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.polygon.io") };
        return new PolygonNewsProvider(httpClient, TestOptionsFactory.CreateDefault(), NullLogger<PolygonNewsProvider>.Instance);
    }

    [Fact]
    public async Task GetRecentNewsAsync_ValidResponse_ReturnsNewsItems()
    {
        var json = """
        {
            "status": "OK",
            "results": [
                {
                    "title": "Apple Reports Strong Earnings",
                    "description": "Revenue exceeded expectations...",
                    "published_utc": "2023-01-20T14:30:00Z",
                    "tickers": ["AAPL"],
                    "publisher": { "name": "Reuters" }
                },
                {
                    "title": "Tech Stocks Rally",
                    "description": "Broad market gains...",
                    "published_utc": "2023-01-19T10:00:00Z",
                    "tickers": ["AAPL", "MSFT"],
                    "publisher": { "name": "Bloomberg" }
                }
            ]
        }
        """;
        var provider = CreateProvider(new MockHttpMessageHandler(json));

        var result = (await provider.GetRecentNewsAsync("AAPL", 5)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("Apple Reports Strong Earnings", result[0].Headline);
        Assert.Equal("Revenue exceeded expectations...", result[0].Summary);
    }

    [Fact]
    public async Task GetRecentNewsAsync_CorrectUrlConstruction()
    {
        var handler = new MockHttpMessageHandler("""{ "status": "OK", "results": [] }""");
        var provider = CreateProvider(handler);

        await provider.GetRecentNewsAsync("AAPL", 5);

        Assert.NotNull(handler.LastRequest);
        var url = handler.LastRequest.RequestUri!.ToString();
        Assert.Contains("ticker=AAPL", url);
        Assert.Contains("published_utc.gte=", url);
        Assert.Contains("apiKey=test-polygon-key", url);
    }

    [Fact]
    public async Task GetRecentNewsAsync_SentimentScore_AlwaysNull()
    {
        var json = """
        {
            "status": "OK",
            "results": [
                {
                    "title": "Some News",
                    "published_utc": "2023-01-20T14:30:00Z",
                    "tickers": ["AAPL"],
                    "publisher": { "name": "Test" }
                }
            ]
        }
        """;
        var provider = CreateProvider(new MockHttpMessageHandler(json));

        var result = (await provider.GetRecentNewsAsync("AAPL", 5)).ToList();

        Assert.Single(result);
        Assert.Null(result[0].SentimentScore);
    }

    [Fact]
    public async Task GetRecentNewsAsync_EmptyResults_ReturnsEmpty()
    {
        var json = """{ "status": "OK", "results": [] }""";
        var provider = CreateProvider(new MockHttpMessageHandler(json));

        var result = await provider.GetRecentNewsAsync("AAPL", 5);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentNewsAsync_HttpError_ReturnsEmpty()
    {
        var provider = CreateProvider(new MockHttpMessageHandler("error", HttpStatusCode.InternalServerError));

        var result = await provider.GetRecentNewsAsync("AAPL", 5);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentNewsAsync_ParsesPublishedUtcCorrectly()
    {
        var json = """
        {
            "status": "OK",
            "results": [
                {
                    "title": "Test",
                    "published_utc": "2023-06-15T09:30:00Z",
                    "tickers": ["AAPL"],
                    "publisher": { "name": "Test" }
                }
            ]
        }
        """;
        var provider = CreateProvider(new MockHttpMessageHandler(json));

        var result = (await provider.GetRecentNewsAsync("AAPL", 5)).ToList();

        Assert.Equal(new DateTime(2023, 6, 15, 9, 30, 0, DateTimeKind.Utc), result[0].PublishedAt);
    }

    [Fact]
    public async Task GetRecentNewsAsync_UsesPublisherName_AsSource()
    {
        var json = """
        {
            "status": "OK",
            "results": [
                {
                    "title": "Test",
                    "published_utc": "2023-01-20T14:30:00Z",
                    "tickers": ["AAPL"],
                    "publisher": { "name": "MarketWatch" }
                }
            ]
        }
        """;
        var provider = CreateProvider(new MockHttpMessageHandler(json));

        var result = (await provider.GetRecentNewsAsync("AAPL", 5)).ToList();

        Assert.Equal("MarketWatch", result[0].Source);
    }
}
