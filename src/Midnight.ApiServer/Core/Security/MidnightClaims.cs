using System.Security.Claims;
using Midnight.Shared;

namespace Midnight.ApiServer.Core.Security;

public static class MidnightClaims
{
    public const string UserId = "sub";
    public const string SessionId = "sid";
    public const string SecurityStamp = "stm";
    public const string ImpersonatorId = "imp";
    public const string Permission = "permission";
    public const string Scope = "scope";
    public const string AuthType = "auth_type";

    public const string AuthTypeSession = "session";
    public const string AuthTypeApiKey = "apikey";
    public const string AuthTypeApplicationKey = "application";

    /// <summary>Id of the application key (only application keys)</summary>
    public const string ApplicationKeyId = "app_key";

    /// <summary>"mda_{identifier}" of the application key, for logs</summary>
    public const string ApplicationKeyIdentifier = "app_key_identifier";

    /// <summary>"{Resource}:{Access}" for every resource the application key can access, e.g. "Servers:ReadWrite"</summary>
    public const string ApplicationAccess = "app_access";
}

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(MidnightClaims.UserId);

        if (!int.TryParse(value, out var id))
            throw new InvalidOperationException("The principal has no user id claim");

        return id;
    }

    public static int? GetSessionId(this ClaimsPrincipal principal)
        => int.TryParse(principal.FindFirstValue(MidnightClaims.SessionId), out var id) ? id : null;

    public static int? GetImpersonatorId(this ClaimsPrincipal principal)
        => int.TryParse(principal.FindFirstValue(MidnightClaims.ImpersonatorId), out var id) ? id : null;

    public static bool IsApiKey(this ClaimsPrincipal principal)
        => principal.FindFirstValue(MidnightClaims.AuthType) == MidnightClaims.AuthTypeApiKey;

    public static bool IsApplicationKey(this ClaimsPrincipal principal)
        => principal.Identity?.IsAuthenticated == true &&
           principal.FindFirstValue(MidnightClaims.AuthType) == MidnightClaims.AuthTypeApplicationKey;

    public static IEnumerable<string> GetPermissions(this ClaimsPrincipal principal)
        => principal.FindAll(MidnightClaims.Permission).Select(x => x.Value);

    /// <summary>
    /// Checks a permission for the current request. Api keys need the permission in their
    /// owner's permissions and in their scopes. Security relevant account actions are never
    /// allowed with api keys or while impersonating.
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal principal, string permission)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return false;

        // Application keys only work on the application api, which checks their resource access instead
        if (principal.IsApplicationKey())
            return false;

        if (!Permissions.Has(principal.GetPermissions(), permission))
            return false;

        var isSecurityAction = Permissions.Matches(Permissions.AccountSecurity + ".*", permission);

        if (principal.IsApiKey())
        {
            if (isSecurityAction)
                return false;

            var scopes = principal.FindAll(MidnightClaims.Scope).Select(x => x.Value);
            return Permissions.Has(scopes, permission);
        }

        if (isSecurityAction && principal.GetImpersonatorId() != null)
            return false;

        return true;
    }
}
