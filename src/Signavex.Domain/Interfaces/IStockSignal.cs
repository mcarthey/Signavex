using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

/// <summary>
/// Contract for all Tier 2 (stock-level) signal implementations.
/// New signals implement this interface and register in DI — no other changes required.
/// </summary>
public interface IStockSignal
{
    string Name { get; }
    double DefaultWeight { get; }
    Task<SignalResult> EvaluateAsync(StockData stock);
}
