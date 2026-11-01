using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Application.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Application;

namespace Midnight.ApiServer.Features.Application.Http;

/// <summary>
/// Application keys for the application api. Only from a regular session: personal api keys,
/// application keys and impersonation can't create or change credentials (403).
/// </summary>
[ApiController]
[Route("api/admin/application-keys")]
[RequireSession]
public class AdminApplicationKeysController : ControllerBase
{
    private readonly ApplicationKeyService _keys;
    private readonly ILogger<AdminApplicationKeysController> _logger;

    public AdminApplicationKeysController(ApplicationKeyService keys, ILogger<AdminApplicationKeysController> logger)
    {
        _keys = keys;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminApiRead)]
    public Task<List<ApplicationKeyResponse>> List(CancellationToken cancellationToken)
        => _keys.ListAsync(cancellationToken);

    /// <summary>The token is only returned once. Granting access needs the matching admin permissions (403 otherwise).</summary>
    [HttpPost]
    [RequirePermission(Permissions.AdminApiWrite)]
    [ProducesResponseType<CreatedApplicationKeyResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ApplicationKeyRequest request, CancellationToken cancellationToken)
    {
        var created = await _keys.CreateAsync(User.GetUserId(), User.GetPermissions().ToList(), request, cancellationToken);
        _logger.LogWarning("User {ActorId} created application key {KeyId} ({Identifier})", User.GetUserId(), created.Key.Id, created.Key.Identifier);

        return Created($"/api/admin/application-keys/{created.Key.Id}", created);
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AdminApiWrite)]
    public async Task<ApplicationKeyResponse> Update(int id, [FromBody] ApplicationKeyRequest request, CancellationToken cancellationToken)
    {
        var key = await _keys.UpdateAsync(id, User.GetPermissions().ToList(), request, cancellationToken);
        _logger.LogWarning("User {ActorId} updated application key {KeyId} ({Identifier})", User.GetUserId(), key.Id, key.Identifier);
        return key;
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminApiWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _keys.DeleteAsync(id, cancellationToken);
        _logger.LogWarning("User {ActorId} deleted application key {KeyId}", User.GetUserId(), id);
        return NoContent();
    }
}
