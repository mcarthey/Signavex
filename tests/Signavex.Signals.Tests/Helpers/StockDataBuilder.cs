using Signavex.Domain.Models;

namespace Signavex.Signals.Tests.Helpers;

internal sealed class StockDataBuilder
{
    private readonly string _ticker;
    private readonly List<OhlcvRecord> _records = new();

    public StockDataBuilder(string ticker = "TEST")
    {
        _ticker = ticker;
    }

    /// <summary>
    /// Adds a flat price series: all OHLC values equal to <paramref name="price"/>.
    /// </summary>
    public StockDataBuilder WithFlatPrices(int days, decimal price, long volume = 1_000_000)
    {
        var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-days));
        for (int i = 0; i < days; i++)
        {
            _records.Add(new OhlcvRecord(
                _ticker,
                startDate.AddDays(i),
                price, price, price, price,
                volume));
        }
        return this;
    }

    /// <summary>
    /// Adds a series where close prices trend linearly from <paramref name="startPrice"/>
    /// to <paramref name="endPrice"/> over <paramref name="days"/>.
    /// </summary>
    public StockDataBuilder WithTrendingPrices(int days, decimal startPrice, decimal endPrice, long volume = 1_000_000)
    {
        var startDate = _records.Count > 0
            ? _records[^1].Date.AddDays(1)
            : DateOnly.FromDateTime(DateTime.Today.AddDays(-days));
        decimal step = (endPrice - startPrice) / (days - 1);

        for (int i = 0; i < days; i++)
        {
            decimal close = startPrice + step * i;
            decimal high = close + 0.50m;
            decimal low = close - 0.50m;
            _records.Add(new OhlcvRecord(
                _ticker,
                startDate.AddDays(i),
                close, high, low, close,
                volume));
        }
        return this;
    }

    /// <summary>
    /// Adds records with explicit OHLCV control per day.
    /// </summary>
    public StockDataBuilder WithRecord(DateOnly date, decimal open, decimal high, decimal low, decimal close, long volume)
    {
        _records.Add(new OhlcvRecord(_ticker, date, open, high, low, close, volume));
        return this;
    }

    /// <summary>
    /// Adds a single record continuing from the last date.
    /// </summary>
    public StockDataBuilder WithDay(decimal open, decimal high, decimal low, decimal close, long volume)
    {
        var date = _records.Count > 0
            ? _records[^1].Date.AddDays(1)
            : DateOnly.FromDateTime(DateTime.Today);
        _records.Add(new OhlcvRecord(_ticker, date, open, high, low, close, volume));
        return this;
    }

    public StockData Build()
    {
        return new StockData(
            _ticker,
            $"{_ticker} Corp",
            _records.AsReadOnly(),
            null,
            Array.Empty<NewsItem>());
    }
}
