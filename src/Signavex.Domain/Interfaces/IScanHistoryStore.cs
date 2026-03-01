using Signavex.Domain.Models;

namespace Signavex.Domain.Interfaces;

public interface IScanHistoryStore
{
    Task<IReadOnlyList<ScanSummary>> GetRecentScansAsync(int count = 30, CancellationToken ct = default);
    Task<CompletedScanResult?> GetScanByIdAsync(string scanId, CancellationToken ct = default);
    Task<TickerHistory?> GetTickerHistoryAsync(string ticker, CancellationToken ct = default);
    Task<IReadOnlyList<(DateTime Date, double Multiplier)>> GetMarketMultiplierTrendAsync(
        int days = 30, CancellationToken ct = default);
}
