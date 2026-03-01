namespace Signavex.Domain.Models;

public record TickerHistory(
    string Ticker,
    string CompanyName,
    IReadOnlyList<TickerAppearance> Appearances
);

public record TickerAppearance(
    string ScanId,
    DateTime ScanDate,
    double RawScore,
    double FinalScore,
    double MarketMultiplier
);
