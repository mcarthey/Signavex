using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

/// <summary>
/// Abstraction over macro-economic data providers (e.g. FRED / Federal Reserve).
/// </summary>
public interface IEconomicDataProvider
{
    Task<MacroIndicators> GetMacroIndicatorsAsync();
}
