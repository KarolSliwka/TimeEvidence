using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TimeEvidence.Security;

public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IConfiguration _config;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration config) : base(options, logger, encoder, clock)
    {
        _config = config;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Allow bypass when disabled via config
        var requireAuth = _config.GetValue<bool?>("Api:RequireAuth") ?? true;
        if (!requireAuth)
        {
            var claims = new[] { new Claim(ClaimTypes.Name, "dev-bypass") };
            var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        // Load configured API key (env vars preferred)
        var configuredKey =
            Environment.GetEnvironmentVariable("Api__Key") ??
            Environment.GetEnvironmentVariable("API_KEY") ??
            _config["Api:Key"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            Logger.LogWarning("API authentication required but no Api:Key configured. Set Api__Key or API_KEY env var.");
            return Task.FromResult(AuthenticateResult.Fail("API key not configured"));
        }

        // Accept either X-Api-Key header or Authorization: ApiKey <key>
        string? presentedKey = null;
        if (Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var headerValue))
        {
            presentedKey = headerValue.ToString();
        }
        else if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var value = authHeader.ToString();
            const string prefix = "ApiKey ";
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                presentedKey = value.Substring(prefix.Length).Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing API key"));
        }

        if (!TimeConstantEquals(presentedKey, configuredKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        var claimsSuccess = new[] { new Claim(ClaimTypes.Name, "api-client") };
        var id = new ClaimsIdentity(claimsSuccess, ApiKeyAuthenticationDefaults.AuthenticationScheme);
        var principalSuccess = new ClaimsPrincipal(id);
        var successTicket = new AuthenticationTicket(principalSuccess, ApiKeyAuthenticationDefaults.AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(successTicket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers["WWW-Authenticate"] = $"ApiKey realm=\"TimeEvidence\", charset=\"UTF-8\"";
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static bool TimeConstantEquals(string a, string b)
    {
        // constant-time comparison
        if (a.Length != b.Length) return false;
        var result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }
}
