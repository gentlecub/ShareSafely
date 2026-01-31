using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ShareSafely.API.Authentication;

/// <summary>
/// API Key authentication scheme options
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string HeaderName = "X-API-Key";
}

/// <summary>
/// API Key authentication handler
/// Validates requests using the X-API-Key header
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyAuthenticationHandler> _logger;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
        _logger = logger.CreateLogger<ApiKeyAuthenticationHandler>();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if API key authentication is enabled
        var apiKeyEnabled = _configuration.GetValue<bool>("Authentication:ApiKey:Enabled", false);
        if (!apiKeyEnabled)
        {
            // If not enabled, allow anonymous access (for development)
            var anonymousClaims = new[]
            {
                new Claim(ClaimTypes.Name, "anonymous"),
                new Claim(ClaimTypes.Role, "User")
            };
            var anonymousIdentity = new ClaimsIdentity(anonymousClaims, Scheme.Name);
            var anonymousPrincipal = new ClaimsPrincipal(anonymousIdentity);
            var anonymousTicket = new AuthenticationTicket(anonymousPrincipal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(anonymousTicket));
        }

        // Try to get API key from header
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var apiKeyHeader))
        {
            _logger.LogWarning("API key header missing from request: {Path}", Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("API key header is missing"));
        }

        var providedApiKey = apiKeyHeader.ToString();

        // Get configured API keys
        var configuredKeys = _configuration.GetSection("Authentication:ApiKey:Keys")
            .Get<ApiKeyConfig[]>() ?? Array.Empty<ApiKeyConfig>();

        var matchingKey = configuredKeys.FirstOrDefault(k =>
            string.Equals(k.Key, providedApiKey, StringComparison.Ordinal));

        if (matchingKey == null)
        {
            _logger.LogWarning("Invalid API key provided for request: {Path}", Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        // Check if key is expired
        if (matchingKey.ExpiresAt.HasValue && matchingKey.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired API key used: {KeyName}", matchingKey.Name);
            return Task.FromResult(AuthenticateResult.Fail("API key has expired"));
        }

        // Create claims principal
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, matchingKey.Name),
            new(ClaimTypes.NameIdentifier, matchingKey.Name),
            new("api_key_name", matchingKey.Name)
        };

        // Add roles
        foreach (var role in matchingKey.Roles ?? Array.Empty<string>())
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        _logger.LogInformation("API key authenticated: {KeyName}", matchingKey.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append("WWW-Authenticate", $"ApiKey realm=\"ShareSafely API\"");
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Configuration for an API key
/// </summary>
public class ApiKeyConfig
{
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string[]? Roles { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Extension methods for API key authentication
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions>? configureOptions = null)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme,
            configureOptions ?? (_ => { }));
    }
}
