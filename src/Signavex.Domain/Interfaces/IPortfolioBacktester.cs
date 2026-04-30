using Signavex.Domain.Models.Portfolio;

namespace Signavex.Domain.Interfaces;

/// <summary>
/// Runs a multi-year, mechanical-strategy portfolio simulation against
/// historical OHLCV data and the live signal-scoring engine. The result
/// includes an equity curve, every trade taken, open positions at end,
/// and aggregate metrics.
/// </summary>
public interface IPortfolioBacktester
{
    Task<PortfolioBacktestResult> RunAsync(PortfolioBacktestRequest request, CancellationToken ct = default);
}
