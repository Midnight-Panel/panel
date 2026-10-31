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
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(UserService userService, ILogger<AdminUsersController> logger)
    {
        _userService = userService;
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
            EnsureCanManageRoles();

        var user = await _userService.CreateAsync(request.Username, request.Email, request.Password, request.RoleIds, cancellationToken);

        _logger.LogInformation("User {ActorId} created user {UserId}", User.GetUserId(), user.Id);

        return UserService.ToAdminResponse(user);
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AdminUsersWrite)]
    public async Task<AdminUserResponse> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var existing = await _userService.GetAsync(id, cancellationToken);

        if (!existing.Roles.Select(x => x.Id).Order().SequenceEqual(request.RoleIds.Distinct().Order()))
            EnsureCanManageRoles();

        var user = await _userService.UpdateAsync(id, request, User.GetUserId(), cancellationToken);

        _logger.LogInformation("User {ActorId} updated user {UserId}", User.GetUserId(), user.Id);

        return UserService.ToAdminResponse(user);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminUsersWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(id, User.GetUserId(), cancellationToken);

        _logger.LogInformation("User {ActorId} deleted user {UserId}", User.GetUserId(), id);

        return NoContent();
    }

    [HttpPost("{id:int}/reset-2fa")]
    [RequirePermission(Permissions.AdminUsersWrite)]
    public async Task<IActionResult> ResetTwoFactor(int id, CancellationToken cancellationToken)
    {
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

    private void EnsureCanManageRoles()
    {
        // Assigning roles is a privilege escalation vector, it needs role write access too
        if (!User.HasPermission(Permissions.AdminRolesWrite))
            throw HttpApiException.Forbidden("Missing permission", $"Changing roles requires '{Permissions.AdminRolesWrite}'");
    }
}
