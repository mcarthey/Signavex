using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Fundamental;

/// <summary>
/// RAT p.170–171 — Stock P/E below industry average suggests potential undervaluation.
/// </summary>
public sealed class PeRatioSignal : IStockSignal
{
    private readonly SignalWeightsOptions _weights;

    public PeRatioSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "PeRatioVsIndustry";
    public double DefaultWeight => _weights.PeRatioVsIndustry;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        if (stock.Fundamentals?.PeRatio is null || stock.Fundamentals.IndustryPeRatio is null)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "P/E data unavailable", false));

        double pe = stock.Fundamentals.PeRatio.Value;
        double industryPe = stock.Fundamentals.IndustryPeRatio.Value;

        if (pe <= 0)
            return Task.FromResult(new SignalResult(Name, -0.5, DefaultWeight,
                $"Negative P/E ({pe:F1}) — company may not be profitable", true));

        double ratio = pe / industryPe;
        double score = ratio switch
        {
            < 0.7 => 1.0,
            < 0.9 => 0.5,
            < 1.1 => 0.0,
            < 1.3 => -0.5,
            _ => -1.0
        };

        string comparison = ratio < 1 ? "below" : "above";
        return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
            $"P/E {pe:F1} is {(1 - ratio):P0} {comparison} industry average of {industryPe:F1}", true));
    }
}
