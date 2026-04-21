using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Signavex.Functions.Orchestrators;
using Signavex.Functions.Security;

namespace Signavex.Functions.Functions;

public class BriefFunctions
{
    private readonly BriefOrchestrator _orchestrator;
    private readonly AdminKeyAuthorizer _authorizer;
    private readonly ILogger<BriefFunctions> _logger;

    public BriefFunctions(
        BriefOrchestrator orchestrator,
        AdminKeyAuthorizer authorizer,
        ILogger<BriefFunctions> logger)
    {
        _orchestrator = orchestrator;
        _authorizer = authorizer;
        _logger = logger;
    }

    // Scheduled brief generation — 11:30 PM UTC on weekdays. The daily scan
    // (10pm UTC) and its possible resume windows typically complete by then.
    // Scan also calls BriefOrchestrator directly on successful completion,
    // so this scheduled run is a safety net.
    [Function("BriefDaily")]
    public async Task BriefDaily(
        [TimerTrigger("0 30 23 * * 1-5")] TimerInfo timer,
        CancellationToken ct)
    {
        _logger.LogInformation("Daily brief timer fired at {Time} UTC", DateTime.UtcNow);
        await _orchestrator.GenerateBriefAsync(ct);
    }

    // Admin-triggered brief generation via HTTP
    [Function("BriefAdminHttp")]
    public async Task<IActionResult> BriefAdminHttp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/generate-brief")] HttpRequest req,
        CancellationToken ct)
    {
        if (!_authorizer.Authorize(req))
            return new UnauthorizedResult();

        _logger.LogInformation("Admin-triggered brief generation via HTTP");
        await _orchestrator.GenerateBriefAsync(ct);
        return new OkResult();
    }
}
