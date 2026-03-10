namespace Signavex.Domain.Models;

public record TickerProfile(
    string Ticker,
    string Name,
    string? Description,
    string? Sector,
    string? Industry,
    string? HomePageUrl,
    long? MarketCap,
    int? Employees
);
