using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Signavex.Functions.Orchestrators;
using Signavex.Functions.Security;

namespace Signavex.Functions.Functions;

public class EconomicSyncFunctions
{
    private readonly EconomicSyncOrchestrator _orchestrator;
    private readonly AdminKeyAuthorizer _authorizer;
    private readonly ILogger<EconomicSyncFunctions> _logger;

    public EconomicSyncFunctions(
        EconomicSyncOrchestrator orchestrator,
        AdminKeyAuthorizer authorizer,
        ILogger<EconomicSyncFunctions> logger)
    {
        _orchestrator = orchestrator;
        _authorizer = authorizer;
        _logger = logger;
    }

    // Economic data sync — 9:30 PM UTC on weekdays (= 4:30 PM ET, after markets close)
    [Function("EconomicSyncDaily")]
    public async Task EconomicSyncDaily(
        [TimerTrigger("0 30 21 * * 1-5")] TimerInfo timer,
        CancellationToken ct)
    {
        _logger.LogInformation("Economic sync timer fired at {Time} UTC", DateTime.UtcNow);
        await _orchestrator.SyncAllSeriesAsync(ct);
    }

    // Admin-triggered economic sync via HTTP
    [Function("EconomicSyncAdminHttp")]
    public async Task<IActionResult> EconomicSyncAdminHttp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ops/sync-economic")] HttpRequest req,
        CancellationToken ct)
    {
        if (!_authorizer.Authorize(req))
            return new UnauthorizedResult();

        _logger.LogInformation("Admin-triggered economic sync via HTTP");
        await _orchestrator.SyncAllSeriesAsync(ct);
        return new OkResult();
    }
}
