using System.Net;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Signavex.Infrastructure.Polygon;

/// <summary>
/// Shared delegating handler that throttles outbound Polygon/Massive API requests
/// using a token bucket rate limiter. Retries once on 429 after respecting Retry-After.
/// </summary>
public class PolygonRateLimitingHandler : DelegatingHandler
{
    private readonly RateLimiter _limiter;
    private readonly ILogger<PolygonRateLimitingHandler> _logger;
    private const int MaxRetries = 2;

    public PolygonRateLimitingHandler(RateLimiter limiter, ILogger<PolygonRateLimitingHandler> logger)
    {
        _limiter = limiter;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            // Acquire a permit from the rate limiter (blocks until available)
            using var lease = await _limiter.AcquireAsync(1, cancellationToken);

            if (!lease.IsAcquired)
            {
                _logger.LogWarning("Rate limiter denied request to {Url}", request.RequestUri);
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
                return response;

            // 429 — parse Retry-After and wait
            var retryAfter = response.Headers.RetryAfter?.Delta
                             ?? TimeSpan.FromSeconds(15);

            if (attempt < MaxRetries)
            {
                _logger.LogWarning(
                    "Rate limited (429) on {Url} — waiting {Seconds:F0}s before retry {Attempt}/{Max}",
                    request.RequestUri, retryAfter.TotalSeconds, attempt + 1, MaxRetries);

                await Task.Delay(retryAfter, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Rate limited (429) on {Url} — max retries exhausted", request.RequestUri);
                return response;
            }
        }

        // Should not reach here, but just in case
        return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
    }

}
