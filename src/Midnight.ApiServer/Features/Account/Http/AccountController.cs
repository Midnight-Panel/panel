using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Account.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Account;

namespace Midnight.ApiServer.Features.Account.Http;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly AccountService _accountService;
    private readonly ApiKeyService _apiKeyService;
    private readonly AvatarService _avatarService;

    public AccountController(AccountService accountService, ApiKeyService apiKeyService, AvatarService avatarService)
    {
        _accountService = accountService;
        _apiKeyService = apiKeyService;
        _avatarService = avatarService;
    }

    [HttpGet]
    [RequirePermission(Permissions.AccountRead)]
    public Task<CurrentUserResponse> Get(CancellationToken cancellationToken)
        => _accountService.GetCurrentAsync(User.GetUserId(), User.GetImpersonatorId(), User.GetPermissions(), cancellationToken);

    [HttpPut("profile")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        await _accountService.UpdateProfileAsync(User.GetUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpPut("avatar")]
    [RequirePermission(Permissions.AccountWrite)]
    [RequestSizeLimit(AvatarService.MaxSize + 64 * 1024)]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        await _avatarService.SetAsync(User.GetUserId(), stream, cancellationToken);
        return NoContent();
    }

    [HttpDelete("avatar")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> RemoveAvatar(CancellationToken cancellationToken)
    {
        await _avatarService.RemoveAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }

    /// <summary>Avatars are public so they can be used in img tags without a token</summary>
    [HttpGet("/api/users/{userId:int}/avatar")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvatar(int userId, CancellationToken cancellationToken)
    {
        var avatar = await _avatarService.GetAsync(userId, cancellationToken);

        if (avatar == null)
            throw HttpApiException.NotFound("No avatar set");

        Response.Headers.CacheControl = "public, max-age=300";
        return PhysicalFile(avatar.Value.Path, avatar.Value.ContentType);
    }

    [HttpPut("password")]
    [RequirePermission(Permissions.AccountSecurity)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _accountService.ChangePasswordAsync(User.GetUserId(), User.GetSessionId(), request, cancellationToken);
        return NoContent();
    }

    [HttpPost("2fa/setup")]
    [RequirePermission(Permissions.AccountSecurity)]
    public Task<TwoFactorSetupResponse> BeginTwoFactorSetup(CancellationToken cancellationToken)
        => _accountService.BeginTwoFactorSetupAsync(User.GetUserId(), cancellationToken);

    [HttpPost("2fa/enable")]
    [RequirePermission(Permissions.AccountSecurity)]
    public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorEnableRequest request, CancellationToken cancellationToken)
    {
        await _accountService.EnableTwoFactorAsync(User.GetUserId(), request.Code, cancellationToken);
        return NoContent();
    }

    [HttpPost("2fa/disable")]
    [RequirePermission(Permissions.AccountSecurity)]
    public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorDisableRequest request, CancellationToken cancellationToken)
    {
        await _accountService.DisableTwoFactorAsync(User.GetUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpGet("sessions")]
    [RequirePermission(Permissions.AccountSecurity)]
    public Task<List<SessionResponse>> ListSessions(CancellationToken cancellationToken)
        => _accountService.ListSessionsAsync(User.GetUserId(), User.GetSessionId(), cancellationToken);

    [HttpDelete("sessions/{id:int}")]
    [RequirePermission(Permissions.AccountSecurity)]
    public async Task<IActionResult> RevokeSession(int id, CancellationToken cancellationToken)
    {
        await _accountService.RevokeSessionAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }

    /// <summary>Logout everywhere, including this session</summary>
    [HttpDelete("sessions")]
    [RequirePermission(Permissions.AccountSecurity)]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken)
    {
        await _accountService.RevokeAllSessionsAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("api-keys")]
    [RequirePermission(Permissions.AccountSecurity)]
    public Task<List<ApiKeyResponse>> ListApiKeys(CancellationToken cancellationToken)
        => _apiKeyService.ListAsync(User.GetUserId(), cancellationToken);

    [HttpPost("api-keys")]
    [RequirePermission(Permissions.AccountSecurity)]
    public Task<CreateApiKeyResponse> CreateApiKey([FromBody] CreateApiKeyRequest request, CancellationToken cancellationToken)
        => _apiKeyService.CreateAsync(User.GetUserId(), User.GetPermissions(), request, cancellationToken);

    [HttpDelete("api-keys/{id:int}")]
    [RequirePermission(Permissions.AccountSecurity)]
    public async Task<IActionResult> DeleteApiKey(int id, CancellationToken cancellationToken)
    {
        await _apiKeyService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
