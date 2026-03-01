using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Sentiment;

/// <summary>
/// RAT p.162 — Analyst rating of Outperform, Outright Buy, or Buy tier is a positive signal.
/// </summary>
public sealed class AnalystRatingSignal : IStockSignal
{
    private static readonly Dictionary<string, double> RatingScores = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Strong Buy", 1.0 },
        { "Outright Buy", 1.0 },
        { "Buy", 0.75 },
        { "Outperform", 0.75 },
        { "Overweight", 0.75 },
        { "Hold", 0.0 },
        { "Neutral", 0.0 },
        { "Equal-weight", 0.0 },
        { "Market Perform", 0.0 },
        { "Underperform", -0.75 },
        { "Underweight", -0.75 },
        { "Sell", -1.0 },
        { "Strong Sell", -1.0 }
    };

    private readonly SignalWeightsOptions _weights;

    public AnalystRatingSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "AnalystRating";
    public double DefaultWeight => _weights.AnalystRating;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        var rating = stock.Fundamentals?.AnalystRating;
        if (string.IsNullOrWhiteSpace(rating))
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "No analyst rating available", false));

        if (RatingScores.TryGetValue(rating, out double score))
            return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
                $"Analyst consensus: {rating}", true));

        return Task.FromResult(new SignalResult(Name, 0, DefaultWeight,
            $"Unrecognized analyst rating: {rating}", false));
    }
}
