using System.Net;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Signavex.Infrastructure.Polygon;

/// <summary>
/// Shared delegating handler that throttles outbound Polygon/Massive API requests
/// using a token bucket rate limiter. On 429 it retries once with a short bounded
/// backoff — if the API is still rate-limited after that, the request fails rather
/// than tying up the caller's HttpClient for ~100s. Long waits cascade badly: three
/// Polygon calls per CandidateDetail page × three retries × 15s waits = page hangs.
/// </summary>
public class PolygonRateLimitingHandler : DelegatingHandler
{
    private readonly RateLimiter _limiter;
    private readonly ILogger<PolygonRateLimitingHandler> _logger;

    // One retry only — callers catch and render partial pages rather than wait
    // through multiple server-dictated backoffs.
    private const int MaxRetries = 1;

    // Cap the Retry-After wait regardless of what the server asks for. If the
    // server says "wait 60s", we still only wait 5s — we'd rather fail fast
    // and let the caller surface a "data unavailable" state than hang 60+ seconds.
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(3);

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
            // Acquire a permit from the rate limiter (blocks until available).
            // The limiter's QueueLimit already caps how long this can wait.
            using var lease = await _limiter.AcquireAsync(1, cancellationToken);

            if (!lease.IsAcquired)
            {
                _logger.LogWarning("Rate limiter denied request to {Url}", request.RequestUri);
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
                return response;

            // 429 from Polygon — honor Retry-After but cap the wait. Dispose the
            // response body before waiting so we don't leak a stream.
            var serverRetryAfter = response.Headers.RetryAfter?.Delta ?? DefaultRetryDelay;
            var retryDelay = serverRetryAfter > MaxRetryDelay ? MaxRetryDelay : serverRetryAfter;
            response.Dispose();

            if (attempt < MaxRetries)
            {
                _logger.LogWarning(
                    "Rate limited (429) on {Url} — waiting {Seconds:F1}s before retry {Attempt}/{Max} " +
                    "(server asked for {ServerSeconds:F0}s; capped)",
                    request.RequestUri, retryDelay.TotalSeconds, attempt + 1, MaxRetries, serverRetryAfter.TotalSeconds);

                await Task.Delay(retryDelay, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "Rate limited (429) on {Url} — retry exhausted, returning 429 to caller",
                    request.RequestUri);
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            }
        }

        return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
    }
}
