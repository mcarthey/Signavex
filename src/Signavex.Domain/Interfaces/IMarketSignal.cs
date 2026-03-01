using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

/// <summary>
/// Contract for all Tier 1 (market-level) signal implementations.
/// These evaluate broad macro conditions and produce a signal that feeds into the MarketContext multiplier.
/// </summary>
public interface IMarketSignal
{
    string Name { get; }
    double DefaultWeight { get; }
    Task<SignalResult> EvaluateAsync(MacroIndicators indicators, IReadOnlyList<OhlcvRecord> spOhlcv);
}
