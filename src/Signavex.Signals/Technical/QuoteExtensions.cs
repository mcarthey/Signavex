using Signavex.Domain.Models;
using Skender.Stock.Indicators;

namespace Signavex.Signals.Technical;

internal static class QuoteExtensions
{
    public static IReadOnlyList<Quote> ToQuotes(this IReadOnlyList<OhlcvRecord> records)
    {
        return records.Select(r => new Quote
        {
            Date = r.Date.ToDateTime(TimeOnly.MinValue),
            Open = r.Open,
            High = r.High,
            Low = r.Low,
            Close = r.Close,
            Volume = r.Volume
        }).ToList();
    }
}
