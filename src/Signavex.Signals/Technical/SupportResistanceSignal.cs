using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Technical;

/// <summary>
/// RAT p.169 — Price near support is a buying opportunity; breaking above resistance confirms breakout.
/// Uses 20-day rolling high/low as the resistance/support proxy.
/// </summary>
public sealed class SupportResistanceSignal : IStockSignal
{
    private const int LookbackDays = 20;
    private const double NearThresholdPercent = 0.03;

    private readonly SignalWeightsOptions _weights;

    public SupportResistanceSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "SupportResistance";
    public double DefaultWeight => _weights.SupportResistance;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        if (stock.OhlcvHistory.Count < LookbackDays + 1)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient data", false));

        var window = stock.OhlcvHistory.SkipLast(1).TakeLast(LookbackDays).ToList();
        var support = (double)window.Min(r => r.Low);
        var resistance = (double)window.Max(r => r.High);
        var currentClose = (double)stock.OhlcvHistory[^1].Close;
        var previousClose = (double)stock.OhlcvHistory[^2].Close;

        bool breakout = previousClose <= resistance && currentClose > resistance;
        bool nearSupport = currentClose <= support * (1 + NearThresholdPercent);
        bool nearResistance = currentClose >= resistance * (1 - NearThresholdPercent);

        if (breakout)
            return Task.FromResult(new SignalResult(Name, 1.0, DefaultWeight,
                $"Breakout above {LookbackDays}-day resistance at {resistance:F2}", true));

        if (nearSupport)
            return Task.FromResult(new SignalResult(Name, 0.75, DefaultWeight,
                $"Near {LookbackDays}-day support ({support:F2}) — potential bounce setup", true));

        if (nearResistance)
            return Task.FromResult(new SignalResult(Name, -0.25, DefaultWeight,
                $"Near {LookbackDays}-day resistance ({resistance:F2}) — watch for rejection or breakout", true));

        return Task.FromResult(new SignalResult(Name, 0, DefaultWeight,
            $"Mid-range: support {support:F2}, resistance {resistance:F2}, current {currentClose:F2}", true));
    }
}
