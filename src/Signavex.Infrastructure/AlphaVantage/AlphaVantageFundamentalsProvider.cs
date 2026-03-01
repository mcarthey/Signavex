using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Infrastructure.AlphaVantage;

/// <summary>
/// Alpha Vantage implementation of IFundamentalsProvider.
/// Full implementation in Phase 2.
/// </summary>
public class AlphaVantageFundamentalsProvider : IFundamentalsProvider
{
    private readonly HttpClient _httpClient;
    private readonly DataProviderOptions _options;

    public AlphaVantageFundamentalsProvider(HttpClient httpClient, IOptions<DataProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<FundamentalsData> GetFundamentalsAsync(string ticker)
        => throw new NotImplementedException("Alpha Vantage fundamentals provider — implemented in Phase 2");
}
