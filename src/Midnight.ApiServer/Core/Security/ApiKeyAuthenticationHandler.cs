using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Database;

namespace Midnight.ApiServer.Core.Security;

/// <summary>Authenticates "Authorization: Bearer mdp_{prefix}_{secret}" requests</summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string KeyPrefix = "mdp_";

    private static readonly TimeSpan LastUsedUpdateInterval = TimeSpan.FromMinutes(1);

    private readonly DataContext _context;
    private readonly UserAuthCache _authCache;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DataContext context,
        UserAuthCache authCache) : base(options, logger, encoder)
    {
        _context = context;
        _authCache = authCache;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = GetBearerToken(Request);

        if (key == null || !key.StartsWith(KeyPrefix, StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        if (!TryParse(key, out var prefix))
            return AuthenticateResult.Fail("Invalid api key");

        var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(x => x.Prefix == prefix, Context.RequestAborted);

        if (apiKey == null || !SecureTokens.HashEquals(key, apiKey.KeyHash))
            return AuthenticateResult.Fail("Invalid api key");

        var now = TimeProvider.GetUtcNow();

        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value <= now)
            return AuthenticateResult.Fail("Api key expired");

        var user = await _authCache.GetAsync(apiKey.UserId, Context.RequestAborted);

        if (user == null || user.IsLocked)
            return AuthenticateResult.Fail("User not found or locked");

        if (apiKey.LastUsedAt == null || now - apiKey.LastUsedAt.Value > LastUsedUpdateInterval)
        {
            apiKey.LastUsedAt = now;
            await _context.SaveChangesAsync(Context.RequestAborted);
        }

        var claims = new List<Claim>
        {
            new(MidnightClaims.UserId, user.Id.ToString()),
            new(MidnightClaims.AuthType, MidnightClaims.AuthTypeApiKey)
        };

        claims.AddRange(user.Permissions.Select(x => new Claim(MidnightClaims.Permission, x)));
        claims.AddRange(apiKey.Scopes.Select(x => new Claim(MidnightClaims.Scope, x)));

        var identity = new ClaimsIdentity(claims, SchemeName, MidnightClaims.UserId, null);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>Extracts the prefix of a key in the format mdp_{prefix}_{secret}</summary>
    public static bool TryParse(string key, out string prefix)
    {
        prefix = "";

        var parts = key.Split('_');

        if (parts.Length != 3 || parts[0] + "_" != KeyPrefix || parts[1].Length == 0 || parts[2].Length == 0)
            return false;

        prefix = parts[1];
        return true;
    }

    public static string? GetBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return header["Bearer ".Length..].Trim();
    }
}
