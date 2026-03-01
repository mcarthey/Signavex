using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Signavex.Infrastructure.Fred;
using Signavex.Infrastructure.Tests.Helpers;

namespace Signavex.Infrastructure.Tests.Fred;

public class FredEconomicDataProviderTests
{
    private const string ValidFedFundsJson = """
    {
        "observations": [
            { "date": "2024-01-01", "value": "5.33" },
            { "date": "2023-12-01", "value": "5.33" }
        ]
    }
    """;

    private const string ValidVixJson = """
    {
        "observations": [
            { "date": "2024-01-15", "value": "13.20" }
        ]
    }
    """;

    private static FredEconomicDataProvider CreateProvider(SequentialMockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.stlouisfed.org") };
        return new FredEconomicDataProvider(httpClient, TestOptionsFactory.CreateDefault(), NullLogger<FredEconomicDataProvider>.Instance);
    }

    [Fact]
    public async Task GetMacroIndicatorsAsync_ValidResponse_ReturnsIndicators()
    {
        var handler = new SequentialMockHttpMessageHandler(
            (ValidFedFundsJson, HttpStatusCode.OK),
            (ValidVixJson, HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetMacroIndicatorsAsync();

        Assert.Equal(5.33, result.FedFundsRate);
        Assert.Equal(5.33, result.FedFundsRatePreviousMonth);
        Assert.Equal(13.20, result.VixLevel);
    }

    [Fact]
    public async Task GetMacroIndicatorsAsync_ParsesFedFundsRate()
    {
        var fedJson = """{ "observations": [{ "date": "2024-01-01", "value": "4.75" }] }""";
        var vixJson = """{ "observations": [] }""";
        var handler = new SequentialMockHttpMessageHandler(
            (fedJson, HttpStatusCode.OK),
            (vixJson, HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetMacroIndicatorsAsync();

        Assert.Equal(4.75, result.FedFundsRate);
    }

    [Fact]
    public async Task GetMacroIndicatorsAsync_ParsesVixLevel()
    {
        var fedJson = """{ "observations": [] }""";
        var vixJson = """{ "observations": [{ "date": "2024-01-15", "value": "18.50" }] }""";
        var handler = new SequentialMockHttpMessageHandler(
            (fedJson, HttpStatusCode.OK),
            (vixJson, HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetMacroIndicatorsAsync();

        Assert.Equal(18.50, result.VixLevel);
    }

    [Fact]
    public async Task GetMacroIndicatorsAsync_MissingValue_Dot_TreatedAsNull()
    {
        var fedJson = """{ "observations": [{ "date": "2024-01-01", "value": "." }] }""";
        var vixJson = """{ "observations": [{ "date": "2024-01-15", "value": "." }] }""";
        var handler = new SequentialMockHttpMessageHandler(
            (fedJson, HttpStatusCode.OK),
            (vixJson, HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetMacroIndicatorsAsync();

        Assert.Null(result.FedFundsRate);
        Assert.Null(result.VixLevel);
    }

    [Fact]
    public async Task GetMacroIndicatorsAsync_HttpError_ReturnsAllNull()
    {
        var handler = new SequentialMockHttpMessageHandler(
            ("error", HttpStatusCode.InternalServerError));
        var provider = CreateProvider(handler);

        var result = await provider.GetMacroIndicatorsAsync();

        Assert.Null(result.FedFundsRate);
        Assert.Null(result.FedFundsRatePreviousMonth);
        Assert.Null(result.VixLevel);
    }

    [Fact]
    public async Task GetMacroIndicatorsAsync_FedFundsPreviousMonth()
    {
        var fedJson = """
        {
            "observations": [
                { "date": "2024-02-01", "value": "5.50" },
                { "date": "2024-01-01", "value": "5.25" }
            ]
        }
        """;
        var vixJson = """{ "observations": [] }""";
        var handler = new SequentialMockHttpMessageHandler(
            (fedJson, HttpStatusCode.OK),
            (vixJson, HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var result = await provider.GetMacroIndicatorsAsync();

        Assert.Equal(5.50, result.FedFundsRate);
        Assert.Equal(5.25, result.FedFundsRatePreviousMonth);
    }

    [Fact]
    public async Task GetMacroIndicatorsAsync_SetsRetrievedAt()
    {
        var handler = new SequentialMockHttpMessageHandler(
            (ValidFedFundsJson, HttpStatusCode.OK),
            (ValidVixJson, HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        var before = DateTime.UtcNow;
        var result = await provider.GetMacroIndicatorsAsync();
        var after = DateTime.UtcNow;

        Assert.InRange(result.RetrievedAt, before, after);
    }
}
