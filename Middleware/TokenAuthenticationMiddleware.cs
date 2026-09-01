using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UserManagementAPI.Middleware;

public class TokenAuthenticationMiddleware
{
    // Documentation (Swagger) is left open; every API route still requires a token.
    private static readonly string[] ExemptPathPrefixes = { "/swagger" };

    private readonly RequestDelegate _next;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;
    private readonly string? _apiToken;

    public TokenAuthenticationMiddleware(RequestDelegate next, ILogger<TokenAuthenticationMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _apiToken = configuration["Authentication:ApiToken"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        if (ExemptPathPrefixes.Any(prefix => path.StartsWithSegments(prefix)))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrEmpty(_apiToken))
        {
            _logger.LogError("Authentication:ApiToken is not configured; rejecting request to {Path}", path);
            await WriteUnauthorized(context, "API authentication is not configured.");
            return;
        }

        var providedToken = ExtractToken(context.Request);

        if (string.IsNullOrEmpty(providedToken) || !TokensMatch(providedToken, _apiToken))
        {
            _logger.LogWarning("Rejected request to {Path}: missing or invalid token", path);
            await WriteUnauthorized(context, "Missing or invalid API token.");
            return;
        }

        await _next(context);
    }

    private static string? ExtractToken(HttpRequest request)
    {
        var header = request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(header))
        {
            return null;
        }

        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }

    private static bool TokensMatch(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private static async Task WriteUnauthorized(HttpContext context, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}
