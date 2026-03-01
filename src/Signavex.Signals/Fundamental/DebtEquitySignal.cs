using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Fundamental;

/// <summary>
/// RAT p.171 — Low debt/equity ratio preferred; high D/E is a risk factor.
/// </summary>
public sealed class DebtEquitySignal : IStockSignal
{
    private readonly SignalWeightsOptions _weights;

    public DebtEquitySignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "DebtEquityRatio";
    public double DefaultWeight => _weights.DebtEquityRatio;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        if (stock.Fundamentals?.DebtToEquityRatio is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "D/E data unavailable", false));

        double de = stock.Fundamentals.DebtToEquityRatio.Value;

        double score = de switch
        {
            < 0.3 => 1.0,
            < 0.6 => 0.5,
            < 1.0 => 0.0,
            < 2.0 => -0.5,
            _ => -1.0
        };

        string assessment = de < 0.6 ? "low (favorable)" : de < 1.0 ? "moderate" : "high (risk factor)";
        return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
            $"Debt/Equity ratio is {de:F2} — {assessment}", true));
    }
}
