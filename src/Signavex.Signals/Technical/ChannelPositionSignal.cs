using Signavex.Domain.Configuration;
using Signavex.Domain.Interfaces;
using Signavex.Domain.Models;
using Microsoft.Extensions.Options;

namespace Signavex.Signals.Technical;

/// <summary>
/// RAT p.169–170 — Price at or near the bottom of an established price channel is a buying signal.
/// Channel is defined by the 20-day high/low range.
/// </summary>
public sealed class ChannelPositionSignal : IStockSignal
{
    private const int LookbackDays = 20;

    private readonly SignalWeightsOptions _weights;

    public ChannelPositionSignal(IOptions<SignavexOptions> options)
    {
        _weights = options.Value.SignalWeights;
    }

    public string Name => "ChannelPosition";
    public double DefaultWeight => _weights.ChannelPosition;

    public Task<SignalResult> EvaluateAsync(StockData stock)
    {
        if (stock.OhlcvHistory.Count < LookbackDays)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "Insufficient data", false));

        var window = stock.OhlcvHistory.TakeLast(LookbackDays).ToList();
        var channelLow = (double)window.Min(r => r.Low);
        var channelHigh = (double)window.Max(r => r.High);
        var currentClose = (double)window[^1].Close;

        double channelRange = channelHigh - channelLow;
        if (channelRange <= 0)
            return Task.FromResult(new SignalResult(Name, 0, DefaultWeight, "No meaningful price channel", false));

        double positionInChannel = (currentClose - channelLow) / channelRange;

        double score = positionInChannel switch
        {
            <= 0.2 => 1.0,
            <= 0.35 => 0.5,
            <= 0.65 => 0.0,
            <= 0.8 => -0.5,
            _ => -1.0
        };

        string positionLabel = positionInChannel switch
        {
            <= 0.2 => "near channel bottom",
            <= 0.35 => "lower portion of channel",
            <= 0.65 => "mid-channel",
            <= 0.8 => "upper portion of channel",
            _ => "near channel top"
        };

        return Task.FromResult(new SignalResult(Name, score, DefaultWeight,
            $"Price is {positionLabel} ({positionInChannel:P0} of range {channelLow:F2}–{channelHigh:F2})", true));
    }
}
