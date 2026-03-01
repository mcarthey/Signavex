using Signavex.Domain.Configuration;
using Signavex.Domain.Enums;
using Signavex.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Signavex.Engine;

/// <summary>
/// Resolves the configured stock universe (SP500/SP400/SP600) to constituent ticker lists.
/// </summary>
public class UniverseProvider
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly SignavexOptions _options;

    public UniverseProvider(IMarketDataProvider marketDataProvider, IOptions<SignavexOptions> options)
    {
        _marketDataProvider = marketDataProvider;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<(string Ticker, MarketTier Tier)>> GetUniverseAsync()
    {
        var result = new List<(string Ticker, MarketTier Tier)>();

        foreach (var universeKey in _options.Universe)
        {
            if (!Enum.TryParse<MarketIndex>(universeKey, ignoreCase: true, out var index))
                continue;

            var tier = index switch
            {
                MarketIndex.SP500 => MarketTier.SP500,
                MarketIndex.SP400 => MarketTier.SP400,
                MarketIndex.SP600 => MarketTier.SP600,
                _ => MarketTier.SP500
            };

            var constituents = await _marketDataProvider.GetIndexConstituentsAsync(index);
            result.AddRange(constituents.Select(t => (t, tier)));
        }

        return result;
    }
}
