using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Application.Services;
using Midnight.Shared;

namespace Midnight.ApiServer.Features.Application.Http;

/// <summary>
/// Base of the application api controllers (/api/application/v1): only application keys, every action declares
/// its access with [ApplicationAccess], rate limited per key and listed in the "application" OpenAPI document.
/// </summary>
[ApiController]
[EnableRateLimiting(ApplicationRateLimit.PolicyName)]
[ApiExplorerSettings(GroupName = OpenApiDocument)]
public abstract class ApplicationControllerBase : ControllerBase
{
    public const string OpenApiDocument = "application";
    public const string RoutePrefix = "api/application/v1";

    /// <summary>"mda_{identifier}" of the calling key, for log messages</summary>
    protected string KeyIdentifier => User.GetApplicationKeyIdentifier();

    /// <summary>The user who created the key, actions are attributed to them. Null once the user was deleted.</summary>
    protected int? CreatorId => User.GetApplicationKeyCreatorId();

    /// <summary>
    /// Permissions of the key's creator, the upper bound for privileged user changes (roles, managing admins).
    /// Without creator only the defaults every user has.
    /// </summary>
    protected async Task<IReadOnlyCollection<string>> GetCreatorPermissionsAsync(UserAuthCache authCache, CancellationToken cancellationToken)
    {
        var creator = CreatorId.HasValue ? await authCache.GetAsync(CreatorId.Value, cancellationToken) : null;

        return creator is { IsLocked: false } ? creator.Permissions : Permissions.UserDefaults;
    }
}
