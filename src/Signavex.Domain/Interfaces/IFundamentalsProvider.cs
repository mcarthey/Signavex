using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

/// <summary>
/// Abstraction over fundamentals data providers (e.g. Alpha Vantage).
/// Fundamentals are cached aggressively — quarterly refresh cadence.
/// </summary>
public interface IFundamentalsProvider
{
    Task<FundamentalsData> GetFundamentalsAsync(string ticker);
}
