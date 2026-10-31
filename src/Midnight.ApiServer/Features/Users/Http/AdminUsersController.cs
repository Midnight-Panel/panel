using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Users.Services;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.Users.Http;

[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly RoleService _roleService;
    private readonly UserAuthCache _authCache;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(UserService userService, RoleService roleService, UserAuthCache authCache, ILogger<AdminUsersController> logger)
    {
        _userService = userService;
        _roleService = roleService;
        _authCache = authCache;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminUsersRead)]
    public Task<PagedResult<AdminUserResponse>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => _userService.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.AdminUsersRead)]
    public async Task<AdminUserResponse> Get(int id, CancellationToken cancellationToken)
        => UserService.ToAdminResponse(await _userService.GetAsync(id, cancellationToken));

    [HttpPost]
    [RequirePermission(Permissions.AdminUsersWrite)]
    public async Task<AdminUserResponse> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (request.RoleIds.Count > 0)
            await EnsureCanAssignRolesAsync(request.RoleIds, cancellationToken);

        var user = await _userService.CreateAsync(request.Username, request.Email, request.Password, request.RoleIds, cancellationToken);

        _logger.LogInformation("User {ActorId} created user {UserId}", User.GetUserId(), user.Id);

        return UserService.ToAdminResponse(user);
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AdminUsersWrite)]
    public async Task<AdminUserResponse> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await EnsureCanManageUserAsync(id, cancellationToken);

        var existing = await _userService.GetAsync(id, cancellationToken);

        if (!existing.Roles.Select(x => x.Id).Order().SequenceEqual(request.RoleIds.Distinct().Order()))
            await EnsureCanAssignRolesAsync(request.RoleIds, cancellationToken);

        var user = await _userService.UpdateAsync(id, request, User.GetUserId(), cancellationToken);

        _logger.LogInformation("User {ActorId} updated user {UserId}", User.GetUserId(), user.Id);

        return UserService.ToAdminResponse(user);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminUsersWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await EnsureCanManageUserAsync(id, cancellationToken);
        await _userService.DeleteAsync(id, User.GetUserId(), cancellationToken);

        _logger.LogInformation("User {ActorId} deleted user {UserId}", User.GetUserId(), id);

        return NoContent();
    }

    [HttpPost("{id:int}/reset-2fa")]
    [RequirePermission(Permissions.AdminUsersWrite)]
    public async Task<IActionResult> ResetTwoFactor(int id, CancellationToken cancellationToken)
    {
        await EnsureCanManageUserAsync(id, cancellationToken);
        await _userService.ResetTwoFactorAsync(id, cancellationToken);

        _logger.LogInformation("User {ActorId} reset 2FA of user {UserId}", User.GetUserId(), id);

        return NoContent();
    }

    /// <summary>
    /// Returns a short lived access token of the target user. The impersonating admin keeps their
    /// own session and returns to it once the token is dropped.
    /// </summary>
    [HttpPost("{id:int}/impersonate")]
    [RequirePermission(Permissions.AdminUsersImpersonate)]
    public async Task<ImpersonationResponse> Impersonate(
        int id,
        [FromServices] UserAuthCache authCache,
        [FromServices] AccessTokenService accessTokenService,
        [FromServices] IOptionsMonitor<AppConfiguration> configuration,
        CancellationToken cancellationToken)
    {
        var actorId = User.GetUserId();

        if (User.GetImpersonatorId() != null || User.IsApiKey())
            throw HttpApiException.Forbidden("Impersonation is only possible from a regular session");

        if (id == actorId)
            throw HttpApiException.BadRequest("You can't impersonate yourself");

        var target = await authCache.GetAsync(id, cancellationToken)
                     ?? throw HttpApiException.NotFound("User not found");

        if (target.IsLocked)
            throw HttpApiException.BadRequest("Locked users can't be impersonated");

        // Prevent privilege escalation: the actor needs every permission the target has
        var actorPermissions = User.GetPermissions().ToList();

        if (target.Permissions.Any(x => !Permissions.Has(actorPermissions, x)))
            throw HttpApiException.Forbidden("You can't impersonate a user with more permissions than yourself");

        var (token, expiresAt) = accessTokenService.Create(
            target.Id,
            target.SecurityStamp,
            null,
            actorId,
            TimeSpan.FromMinutes(configuration.CurrentValue.Authentication.ImpersonationMinutes));

        _logger.LogWarning("User {ActorId} started impersonating user {UserId}", actorId, target.Id);

        return new ImpersonationResponse { AccessToken = token, ExpiresAt = expiresAt };
    }

    private async Task EnsureCanAssignRolesAsync(IEnumerable<int> roleIds, CancellationToken cancellationToken)
    {
        // Assigning roles is a privilege escalation vector, it needs role write access too
        if (!User.HasPermission(Permissions.AdminRolesWrite))
            throw HttpApiException.Forbidden("Missing permission", $"Changing roles requires '{Permissions.AdminRolesWrite}'");

        // ... and only roles whose permissions the actor has himself (e.g. no Administrators role for a moderator)
        var ids = roleIds.ToHashSet();
        var actorPermissions = User.GetPermissions().ToList();
        var roles = await _roleService.ListAsync(cancellationToken);

        if (roles.Where(x => ids.Contains(x.Id)).SelectMany(x => x.Permissions).Any(x => !Permissions.Has(actorPermissions, x)))
            throw HttpApiException.Forbidden("Missing permission", "You can't assign a role with permissions you don't have yourself");
    }

    /// <summary>
    /// Changing, deleting or resetting 2FA of a user with more permissions than the actor would allow taking
    /// over that account (e.g. setting the password of an administrator)
    /// </summary>
    private async Task EnsureCanManageUserAsync(int id, CancellationToken cancellationToken)
    {
        var target = await _authCache.GetAsync(id, cancellationToken)
                     ?? throw HttpApiException.NotFound("User not found");

        var actorPermissions = User.GetPermissions().ToList();

        if (target.Permissions.Any(x => !Permissions.Has(actorPermissions, x)))
            throw HttpApiException.Forbidden("You can't manage a user with more permissions than yourself");
    }
}
