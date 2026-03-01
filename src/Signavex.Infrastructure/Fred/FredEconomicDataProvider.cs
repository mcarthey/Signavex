using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Infrastructure.Fred;

/// <summary>
/// FRED (Federal Reserve Economic Data) implementation of IEconomicDataProvider.
/// Full implementation in Phase 2.
/// </summary>
public class FredEconomicDataProvider : IEconomicDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly DataProviderOptions _options;

    public FredEconomicDataProvider(HttpClient httpClient, IOptions<DataProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<MacroIndicators> GetMacroIndicatorsAsync()
        => throw new NotImplementedException("FRED economic data provider — implemented in Phase 2");
}
