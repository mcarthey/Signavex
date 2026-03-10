using Signavex.Domain.Enums;
using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

/// <summary>
/// Abstraction over OHLCV and index constituent data providers (e.g. Polygon.io).
/// Swap implementations without touching business logic.
/// </summary>
public interface IMarketDataProvider
{
    Task<IEnumerable<OhlcvRecord>> GetDailyOhlcvAsync(string ticker, int days);
    Task<IEnumerable<string>> GetIndexConstituentsAsync(MarketIndex index);
    Task<TickerProfile?> GetTickerProfileAsync(string ticker);
}
