using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Auth.Services;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.ApiServer.Features.Users.Services;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Admin;
using Midnight.Shared.Http.Application;

namespace Midnight.ApiServer.Features.Application.Http;

/// <summary>
/// Users. Changing or deleting users with more permissions than the key's creator and assigning roles
/// beyond the creator's permissions is refused (403), so a key can't take over administrator accounts.
/// </summary>
[Route(RoutePrefix + "/users")]
public class ApplicationUsersController : ApplicationControllerBase
{
    private readonly UserService _users;
    private readonly UserAuthCache _authCache;
    private readonly ILogger<ApplicationUsersController> _logger;

    public ApplicationUsersController(UserService users, UserAuthCache authCache, ILogger<ApplicationUsersController> logger)
    {
        _users = users;
        _authCache = authCache;
        _logger = logger;
    }

    /// <summary>search = username or email contains, the filters are exact matches</summary>
    [HttpGet]
    [ApplicationAccess(ApplicationResource.Users, ApplicationAccess.Read)]
    public Task<PagedResult<AdminUserResponse>> List(
        [FromQuery] PagedQuery query,
        [FromQuery(Name = "filter[email]")] string? email,
        [FromQuery(Name = "filter[username]")] string? username,
        [FromQuery(Name = "filter[externalId]")] string? externalId,
        CancellationToken cancellationToken)
        => _users.ListAsync(query, new UserListFilter(email, username, externalId), cancellationToken);

    [HttpGet("{id:int}")]
    [ApplicationAccess(ApplicationResource.Users, ApplicationAccess.Read)]
    public async Task<AdminUserResponse> Get(int id, CancellationToken cancellationToken)
        => UserService.ToAdminResponse(await _users.GetAsync(id, cancellationToken));

    [HttpGet("external/{externalId}")]
    [ApplicationAccess(ApplicationResource.Users, ApplicationAccess.Read)]
    public async Task<AdminUserResponse> GetByExternalId(string externalId, CancellationToken cancellationToken)
        => UserService.ToAdminResponse(await _users.GetByExternalIdAsync(externalId, cancellationToken));

    /// <summary>Without password a random one is set (PasswordGenerated = true), SendPasswordReset mails a link to choose one</summary>
    [HttpPost]
    [ApplicationAccess(ApplicationResource.Users, ApplicationAccess.ReadWrite)]
    [ProducesResponseType<ApplicationCreatedUserResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ApplicationCreateUserRequest request, [FromServices] AuthService authService, CancellationToken cancellationToken)
    {
        if (request.RoleIds.Count > 0)
            await EnsureCanAssignRolesAsync(request.RoleIds, cancellationToken);

        var passwordGenerated = string.IsNullOrEmpty(request.Password);
        var password = passwordGenerated ? SecureTokens.Generate(24) : request.Password!;

        var user = await _users.CreateAsync(request.Username, request.Email, password, request.RoleIds, request.ExternalId, cancellationToken);

        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) created user {UserId}", KeyIdentifier, CreatorId, user.Id);

        if (request.SendPasswordReset)
            await authService.RequestPasswordResetAsync(user.Email, cancellationToken);

        return Created($"/{RoutePrefix}/users/{user.Id}", ToCreatedResponse(user, passwordGenerated));
    }

    [HttpPatch("{id:int}")]
    [ApplicationAccess(ApplicationResource.Users, ApplicationAccess.ReadWrite)]
    public async Task<AdminUserResponse> Update(int id, [FromBody] ApplicationUpdateUserRequest request, CancellationToken cancellationToken)
    {
        await _users.EnsureCanManageAsync(id, await GetCreatorPermissionsAsync(_authCache, cancellationToken), cancellationToken);

        var existing = await _users.GetAsync(id, cancellationToken);
        var currentRoles = existing.Roles.Select(x => x.Id).ToList();

        if (request.RoleIds != null && !currentRoles.Order().SequenceEqual(request.RoleIds.Distinct().Order()))
            await EnsureCanAssignRolesAsync(request.RoleIds, cancellationToken);

        var update = new UpdateUserRequest
        {
            Username = request.Username ?? existing.Username,
            Email = request.Email ?? existing.Email,
            Password = string.IsNullOrEmpty(request.Password) ? null : request.Password,
            IsLocked = request.IsLocked ?? existing.IsLocked,
            RoleIds = request.RoleIds ?? currentRoles
        };

        var externalId = request.ExternalId == null ? null : new ExternalIdChange(request.ExternalId);

        // 0 = no acting panel user, the "not yourself" checks only apply to the creator of the key
        var user = await _users.UpdateAsync(id, update, CreatorId ?? 0, externalId, cancellationToken);

        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) updated user {UserId}", KeyIdentifier, CreatorId, id);

        return UserService.ToAdminResponse(user);
    }

    /// <summary>409 while the user still owns servers</summary>
    [HttpDelete("{id:int}")]
    [ApplicationAccess(ApplicationResource.Users, ApplicationAccess.ReadWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _users.EnsureCanManageAsync(id, await GetCreatorPermissionsAsync(_authCache, cancellationToken), cancellationToken);
        await _users.DeleteAsync(id, CreatorId ?? 0, cancellationToken);

        _logger.LogWarning("Application key {KeyIdentifier} (created by user {CreatorId}) deleted user {UserId}", KeyIdentifier, CreatorId, id);

        return NoContent();
    }

    /// <summary>Like the admin page: needs admin.roles.write and every permission of the roles, here from the key's creator</summary>
    private async Task EnsureCanAssignRolesAsync(IEnumerable<int> roleIds, CancellationToken cancellationToken)
    {
        var permissions = await GetCreatorPermissionsAsync(_authCache, cancellationToken);

        if (!Permissions.Has(permissions, Permissions.AdminRolesWrite))
            throw HttpApiException.Forbidden("Missing permission", $"Assigning roles requires the creator of the application key to have '{Permissions.AdminRolesWrite}'");

        await _users.EnsureCanAssignRolesAsync(roleIds, permissions, cancellationToken);
    }

    private static ApplicationCreatedUserResponse ToCreatedResponse(User user, bool passwordGenerated)
    {
        var admin = UserService.ToAdminResponse(user);

        return new ApplicationCreatedUserResponse
        {
            Id = admin.Id,
            Username = admin.Username,
            Email = admin.Email,
            HasAvatar = admin.HasAvatar,
            IsLocked = admin.IsLocked,
            TwoFactorEnabled = admin.TwoFactorEnabled,
            RoleIds = admin.RoleIds,
            RoleNames = admin.RoleNames,
            CreatedAt = admin.CreatedAt,
            LastLoginAt = admin.LastLoginAt,
            ExternalId = admin.ExternalId,
            PasswordGenerated = passwordGenerated
        };
    }
}
