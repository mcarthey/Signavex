using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Signavex.Functions.Orchestrators;
using Signavex.Functions.Security;

namespace Signavex.Functions.Functions;

public class ScanFunctions
{
    private readonly ScanOrchestrator _orchestrator;
    private readonly AdminKeyAuthorizer _authorizer;
    private readonly ILogger<ScanFunctions> _logger;

    public ScanFunctions(
        ScanOrchestrator orchestrator,
        AdminKeyAuthorizer authorizer,
        ILogger<ScanFunctions> logger)
    {
        _orchestrator = orchestrator;
        _authorizer = authorizer;
        _logger = logger;
    }

    // Daily scan — 10:00 PM UTC on weekdays (Mon-Fri), skips weekends.
    // NCRONTAB: {second} {minute} {hour} {day} {month} {day-of-week}
    [Function("ScanDaily")]
    public async Task ScanDaily(
        [TimerTrigger("0 0 22 * * 1-5")] TimerInfo timer,
        CancellationToken ct)
    {
        _logger.LogInformation("Daily scan timer fired at {Time} UTC", DateTime.UtcNow);
        await _orchestrator.RunScanAsync(ct);
    }

    // Continuation timer — every 30 min between 10:30pm and 5:30am UTC.
    // If a scan was interrupted by the 10-min Functions execution limit, the
    // checkpoint is present and this resumes it. Completes in chunks of up to
    // 10 min each until the scan finishes.
    [Function("ScanResume")]
    public async Task ScanResume(
        [TimerTrigger("0 30 22-5 * * *")] TimerInfo timer,
        CancellationToken ct)
    {
        if (await _orchestrator.HasResumableCheckpointAsync(ct))
        {
            _logger.LogInformation("Resumable scan checkpoint found — continuing");
            await _orchestrator.RunScanAsync(ct);
        }
    }

    // Admin-triggered scan via HTTP from the Web app
    [Function("ScanAdminHttp")]
    public async Task<IActionResult> ScanAdminHttp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ops/scan")] HttpRequest req,
        CancellationToken ct)
    {
        if (!_authorizer.Authorize(req))
            return new UnauthorizedResult();

        _logger.LogInformation("Admin-triggered scan via HTTP");

        // Fire-and-forget — scan may exceed the HTTP request lifetime;
        // we return immediately and the scan runs until the Function times out.
        // If interrupted, ScanResume picks it up.
        _ = Task.Run(async () =>
        {
            try { await _orchestrator.RunScanAsync(CancellationToken.None); }
            catch (Exception ex) { _logger.LogError(ex, "Background scan failed"); }
        }, ct);

        return new AcceptedResult();
    }
}
