using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Midnight.Shared.Http.Application;

namespace Midnight.ApiServer.Core.Security;

/// <summary>
/// Application api endpoint: only application keys (scheme <see cref="ApplicationKeyAuthenticationHandler.SchemeName"/>)
/// with at least the given access to the resource, e.g. [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.ReadWrite)].
/// Other tokens (access tokens, personal api keys) are not authenticated on these endpoints (401), missing access is 403.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ApplicationAccessAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public ApplicationResource Resource { get; }
    public ApplicationAccess Access { get; }

    public ApplicationAccessAttribute(ApplicationResource resource, ApplicationAccess access)
    {
        Resource = resource;
        Access = access;
        AuthenticationSchemes = ApplicationKeyAuthenticationHandler.SchemeName;
    }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new ApplicationAccessRequirement(Resource, Access);
    }
}

public record ApplicationAccessRequirement(ApplicationResource Resource, ApplicationAccess Access) : IAuthorizationRequirement;

public class ApplicationAccessAuthorizationHandler : AuthorizationHandler<ApplicationAccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApplicationAccessRequirement requirement)
    {
        if (context.User.HasApplicationAccess(requirement.Resource, requirement.Access))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Requires a regular login session: personal api keys, application keys and impersonation sessions are refused (403).
/// Used for actions that create credentials, like managing application keys.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireSessionAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new SessionRequirement();
    }
}

public record SessionRequirement : IAuthorizationRequirement;

public class SessionAuthorizationHandler : AuthorizationHandler<SessionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SessionRequirement requirement)
    {
        if (context.User.IsRegularSession())
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

public static class ApplicationClaimsExtensions
{
    public static ApplicationAccess GetApplicationAccess(this ClaimsPrincipal principal, ApplicationResource resource)
    {
        if (!principal.IsApplicationKey())
            return ApplicationAccess.None;

        var prefix = resource + ":";
        var best = ApplicationAccess.None;

        foreach (var claim in principal.FindAll(MidnightClaims.ApplicationAccess))
        {
            if (!claim.Value.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            if (Enum.TryParse<ApplicationAccess>(claim.Value[prefix.Length..], out var access) && Enum.IsDefined(access) && access > best)
                best = access;
        }

        return best;
    }

    public static bool HasApplicationAccess(this ClaimsPrincipal principal, ApplicationResource resource, ApplicationAccess required)
        => principal.IsApplicationKey() && principal.GetApplicationAccess(resource) >= required;

    public static int? GetApplicationKeyId(this ClaimsPrincipal principal)
        => principal.IsApplicationKey() && int.TryParse(principal.FindFirstValue(MidnightClaims.ApplicationKeyId), out var id) ? id : null;

    /// <summary>The creator of the application key, null once deleted</summary>
    public static int? GetApplicationKeyCreatorId(this ClaimsPrincipal principal)
        => principal.IsApplicationKey() && int.TryParse(principal.FindFirstValue(MidnightClaims.UserId), out var id) ? id : null;

    /// <summary>"mda_{identifier}" for log messages</summary>
    public static string GetApplicationKeyIdentifier(this ClaimsPrincipal principal)
        => principal.FindFirstValue(MidnightClaims.ApplicationKeyIdentifier) ?? "unknown";

    /// <summary>Logged in with username and password: no api key, no application key, not impersonating</summary>
    public static bool IsRegularSession(this ClaimsPrincipal principal)
        => principal.Identity?.IsAuthenticated == true &&
           principal.FindFirstValue(MidnightClaims.AuthType) == MidnightClaims.AuthTypeSession &&
           principal.FindFirstValue(MidnightClaims.UserId) != null &&
           principal.GetImpersonatorId() == null;
}
