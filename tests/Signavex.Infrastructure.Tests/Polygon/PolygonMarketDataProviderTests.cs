using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Signavex.Domain.Enums;
using Signavex.Infrastructure.Polygon;
using Signavex.Infrastructure.Tests.Helpers;

namespace Signavex.Infrastructure.Tests.Polygon;

public class PolygonMarketDataProviderTests
{
    private static PolygonMarketDataProvider CreateProvider(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.polygon.io") };
        return new PolygonMarketDataProvider(httpClient, TestOptionsFactory.CreateDefault(), NullLogger<PolygonMarketDataProvider>.Instance);
    }

    [Fact]
    public async Task GetDailyOhlcvAsync_ValidResponse_ReturnsOhlcvRecords()
    {
        var json = """
        {
            "status": "OK",
            "resultsCount": 2,
            "results": [
                { "t": 1674000000000, "o": 130.00, "h": 132.00, "l": 129.00, "c": 131.50, "v": 50000000 },
                { "t": 1674086400000, "o": 131.50, "h": 133.00, "l": 130.00, "c": 132.00, "v": 45000000 }
            ]
        }
        """;
        var provider = CreateProvider(new MockHttpMessageHandler(json));

        var result = (await provider.GetDailyOhlcvAsync("AAPL", 30)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("AAPL", result[0].Ticker);
        Assert.Equal(130.00m, result[0].Open);
        Assert.Equal(132.00m, result[0].High);
        Assert.Equal(129.00m, result[0].Low);
        Assert.Equal(131.50m, result[0].Close);
        Assert.Equal(50000000L, result[0].Volume);
    }

    [Fact]
    public async Task GetDailyOhlcvAsync_CorrectUrlConstruction()
    {
        var handler = new MockHttpMessageHandler("""{ "status": "OK", "results": [] }""");
        var provider = CreateProvider(handler);

        await provider.GetDailyOhlcvAsync("MSFT", 30);

        Assert.NotNull(handler.LastRequest);
        var url = handler.LastRequest.RequestUri!.ToString();
        Assert.Contains("/v2/aggs/ticker/MSFT/range/1/day/", url);
        Assert.Contains("apiKey=test-polygon-key", url);
        Assert.Contains("sort=asc", url);
        Assert.Contains("adjusted=true", url);
    }

    [Fact]
    public async Task GetDailyOhlcvAsync_EmptyResults_ReturnsEmptyCollection()
    {
        var json = """{ "status": "OK", "resultsCount": 0, "results": [] }""";
        var provider = CreateProvider(new MockHttpMessageHandler(json));

        var result = await provider.GetDailyOhlcvAsync("AAPL", 30);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDailyOhlcvAsync_NullResults_ReturnsEmptyCollection()
    {
        var json = """{ "status": "OK", "resultsCount": 0 }""";
        var provider = CreateProvider(new MockHttpMessageHandler(json));

        var result = await provider.GetDailyOhlcvAsync("AAPL", 30);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDailyOhlcvAsync_HttpError_ReturnsEmptyCollection()
    {
        var provider = CreateProvider(new MockHttpMessageHandler("error", HttpStatusCode.InternalServerError));

        var result = await provider.GetDailyOhlcvAsync("AAPL", 30);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDailyOhlcvAsync_MapsTimestampToDateOnly()
    {
        // 1674000000000 ms = 2023-01-18 UTC
        var json = """
        {
            "status": "OK",
            "results": [{ "t": 1674000000000, "o": 1, "h": 1, "l": 1, "c": 1, "v": 1 }]
        }
        """;
        var provider = CreateProvider(new MockHttpMessageHandler(json));

        var result = (await provider.GetDailyOhlcvAsync("AAPL", 30)).ToList();

        Assert.Single(result);
        Assert.Equal(new DateOnly(2023, 1, 18), result[0].Date);
    }

    [Fact]
    public async Task GetIndexConstituentsAsync_SP500_ReturnsTickers()
    {
        // Uses embedded resource - no HTTP mock needed
        var handler = new MockHttpMessageHandler("{}");
        var provider = CreateProvider(handler);

        var result = (await provider.GetIndexConstituentsAsync(MarketIndex.SP500)).ToList();

        Assert.NotEmpty(result);
        Assert.Contains("AAPL", result);
        Assert.Contains("MSFT", result);
    }

    [Fact]
    public async Task GetIndexConstituentsAsync_SP400_ReturnsTickers()
    {
        var handler = new MockHttpMessageHandler("{}");
        var provider = CreateProvider(handler);

        var result = (await provider.GetIndexConstituentsAsync(MarketIndex.SP400)).ToList();

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetIndexConstituentsAsync_SP600_ReturnsTickers()
    {
        var handler = new MockHttpMessageHandler("{}");
        var provider = CreateProvider(handler);

        var result = (await provider.GetIndexConstituentsAsync(MarketIndex.SP600)).ToList();

        Assert.NotEmpty(result);
    }
}
