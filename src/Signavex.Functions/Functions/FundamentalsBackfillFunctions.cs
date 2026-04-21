using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Signavex.Functions.Orchestrators;

namespace Signavex.Functions.Functions;

public class FundamentalsBackfillFunctions
{
    private readonly FundamentalsBackfillOrchestrator _orchestrator;
    private readonly ILogger<FundamentalsBackfillFunctions> _logger;

    public FundamentalsBackfillFunctions(
        FundamentalsBackfillOrchestrator orchestrator,
        ILogger<FundamentalsBackfillFunctions> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    // Fundamentals backfill — 1:00 AM UTC daily. Alpha Vantage free tier
    // resets at midnight UTC; running at 1am gives it 23 hours of the day
    // to accept our ~24 calls without hitting the 25/day limit.
    //
    // Each cycle processes up to 12 tickers (24 AV calls), rate-limited to
    // ~2.4 calls/min. So a full cycle is ~5 minutes — fits easily in the
    // 10-min Consumption plan execution limit.
    [Function("FundamentalsBackfillDaily")]
    public async Task FundamentalsBackfillDaily(
        [TimerTrigger("0 0 1 * * *")] TimerInfo timer,
        CancellationToken ct)
    {
        _logger.LogInformation("Fundamentals backfill timer fired at {Time} UTC", DateTime.UtcNow);
        var filled = await _orchestrator.RunBackfillCycleAsync(ct);
        _logger.LogInformation("Fundamentals backfill cycle complete: {Count} tickers cached", filled);
    }
}
