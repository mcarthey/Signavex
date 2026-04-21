using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Signavex.Functions.Security;

/// <summary>
/// Shared-secret authorization for HTTP-triggered admin operations.
/// The Web app includes an `x-signavex-admin-key` header on calls to admin
/// endpoints; this class checks that header against the `Signavex:FunctionAdminKey`
/// config value (wired in by Terraform as an app setting on both sides).
///
/// Functions are registered as AuthLevel.Anonymous so Azure's built-in
/// function keys are bypassed — the shared header is the sole gate.
/// </summary>
public class AdminKeyAuthorizer
{
    private readonly string? _adminKey;
    private readonly ILogger<AdminKeyAuthorizer> _logger;

    public const string HeaderName = "x-signavex-admin-key";

    public AdminKeyAuthorizer(IConfiguration configuration, ILogger<AdminKeyAuthorizer> logger)
    {
        _adminKey = configuration["Signavex:FunctionAdminKey"];
        _logger = logger;

        if (string.IsNullOrEmpty(_adminKey))
        {
            _logger.LogWarning(
                "Signavex:FunctionAdminKey is not configured — admin HTTP endpoints will reject all requests");
        }
    }

    public bool Authorize(HttpRequest request)
    {
        if (string.IsNullOrEmpty(_adminKey))
            return false;

        if (!request.Headers.TryGetValue(HeaderName, out var value))
            return false;

        var provided = value.ToString();
        if (string.IsNullOrEmpty(provided))
            return false;

        // Constant-time comparison to avoid timing attacks on the shared key
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(provided),
            System.Text.Encoding.UTF8.GetBytes(_adminKey));
    }
}
