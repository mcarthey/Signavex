namespace Signavex.Infrastructure.AlphaVantage;

internal static class SectorPeAverages
{
    private const double SP500LongTermAverage = 20.0;

    private static readonly Dictionary<string, double> Averages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TECHNOLOGY"] = 28.0,
        ["FINANCE"] = 14.0,
        ["LIFE SCIENCES"] = 22.0,
        ["MANUFACTURING"] = 20.0,
        ["TRADE & SERVICES"] = 22.0,
        ["ENERGY & TRANSPORTATION"] = 16.0,
        ["REAL ESTATE & CONSTRUCTION"] = 25.0,
        ["MINERAL RESOURCES"] = 17.0,
    };

    public static double? Lookup(string? sector)
    {
        if (string.IsNullOrWhiteSpace(sector))
            return null;

        return Averages.TryGetValue(sector.Trim(), out var pe)
            ? pe
            : SP500LongTermAverage;
    }
}
