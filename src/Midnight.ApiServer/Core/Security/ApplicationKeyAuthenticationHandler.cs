using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Database;
using Midnight.Shared.Http.Application;

namespace Midnight.ApiServer.Core.Security;

/// <summary>
/// Authenticates "Authorization: Bearer mda_{identifier}_{secret}" application keys. They are only accepted
/// on /api/application (everywhere else the request fails with 401), the client ip has to match the allowed ips of the key.
/// The principal carries the resource access of the key and, when the creator still exists, the creator as user id
/// so activity entries are attributed to them.
/// </summary>
public class ApplicationKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApplicationKey";
    public const string KeyPrefix = ApplicationApi.TokenPrefix;
    public const string PathPrefix = "/api/application";

    private static readonly TimeSpan LastUsedUpdateInterval = TimeSpan.FromMinutes(1);

    private readonly DataContext _context;

    public ApplicationKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DataContext context) : base(options, logger, encoder)
    {
        _context = context;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = ApiKeyAuthenticationHandler.GetBearerToken(Request);

        if (key == null || !key.StartsWith(KeyPrefix, StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        if (!IsApplicationPath(Request.Path))
            return AuthenticateResult.Fail("Application keys can only be used for the application api");

        if (!TryParse(key, out var identifier))
            return AuthenticateResult.Fail("Invalid application key");

        var applicationKey = await _context.ApplicationKeys.FirstOrDefaultAsync(x => x.Identifier == identifier, Context.RequestAborted);

        if (applicationKey == null || !SecureTokens.HashEquals(key, applicationKey.KeyHash))
            return AuthenticateResult.Fail("Invalid application key");

        // RemoteIpAddress is the client after UseForwardedHeaders (trusted proxies only)
        if (!IpRules.IsAllowed(Context.Connection.RemoteIpAddress, applicationKey.AllowedIps))
        {
            Logger.LogWarning("Application key {Identifier} was used from the not allowed ip {IpAddress}", identifier, Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("The application key can't be used from this ip address");
        }

        var now = TimeProvider.GetUtcNow();

        if (applicationKey.LastUsedAt == null || now - applicationKey.LastUsedAt.Value > LastUsedUpdateInterval)
        {
            applicationKey.LastUsedAt = now;
            await _context.SaveChangesAsync(Context.RequestAborted);
        }

        var claims = new List<Claim>
        {
            new(MidnightClaims.AuthType, MidnightClaims.AuthTypeApplicationKey),
            new(MidnightClaims.ApplicationKeyId, applicationKey.Id.ToString()),
            new(MidnightClaims.ApplicationKeyIdentifier, KeyPrefix + applicationKey.Identifier)
        };

        if (applicationKey.CreatedById.HasValue)
            claims.Add(new Claim(MidnightClaims.UserId, applicationKey.CreatedById.Value.ToString()));

        claims.AddRange(applicationKey.GetPermissions()
            .Where(x => x.Value != ApplicationAccess.None)
            .Select(x => new Claim(MidnightClaims.ApplicationAccess, $"{x.Key}:{x.Value}")));

        var identity = new ClaimsIdentity(claims, SchemeName, MidnightClaims.ApplicationKeyIdentifier, null);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    public static bool IsApplicationPath(PathString path)
        => path.StartsWithSegments(PathPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Extracts the identifier of a key in the format mda_{identifier}_{secret}</summary>
    public static bool TryParse(string key, out string identifier)
    {
        identifier = "";

        var parts = key.Split('_');

        if (parts.Length != 3 || parts[0] + "_" != KeyPrefix || parts[1].Length == 0 || parts[2].Length == 0)
            return false;

        identifier = parts[1];
        return true;
    }
}
