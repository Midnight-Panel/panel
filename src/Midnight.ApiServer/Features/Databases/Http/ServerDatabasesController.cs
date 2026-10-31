using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Databases.Entities;
using Midnight.ApiServer.Features.Databases.Services;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Databases.Http;

/// <summary>MySQL databases of a server. Passwords are only returned with database.view-password.</summary>
[ApiController]
[Route("api/servers/{uuid:guid}/databases")]
[RequirePermission(Permissions.AccountRead)]
public class ServerDatabasesController : ControllerBase
{
    private readonly ServerAccessService _access;
    private readonly ServerDatabaseService _databases;
    private readonly ActivityService _activity;

    public ServerDatabasesController(ServerAccessService access, ServerDatabaseService databases, ActivityService activity)
    {
        _access = access;
        _databases = databases;
        _activity = activity;
    }

    [HttpGet]
    public async Task<ServerDatabaseListResponse> List(Guid uuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.DatabaseRead, cancellationToken);
        return await _databases.ListAsync(access.Server, access.Has(ServerPermissions.DatabaseViewPassword), cancellationToken);
    }

    /// <summary>409: databases disabled, limit reached, name taken, no database host available. 502: the host failed.</summary>
    [HttpPost]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ServerDatabaseResponse> Create(Guid uuid, [FromBody] CreateServerDatabaseRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.DatabaseCreate, cancellationToken);
        var database = await _databases.CreateAsync(access.Server, request, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.DatabaseCreate, Describe(database));

        return _databases.ToResponse(database, access.Has(ServerPermissions.DatabaseViewPassword));
    }

    [HttpPost("{id:int}/rotate-password")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ServerDatabaseResponse> RotatePassword(Guid uuid, int id, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.DatabaseUpdate, cancellationToken);
        var database = await _databases.RotatePasswordAsync(access.Server, id, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.DatabaseRotatePassword, Describe(database));

        return _databases.ToResponse(database, access.Has(ServerPermissions.DatabaseViewPassword));
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Delete(Guid uuid, int id, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.DatabaseDelete, cancellationToken);
        var database = await _databases.DeleteAsync(access.Server, id, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.DatabaseDelete, Describe(database));

        return NoContent();
    }

    private static Dictionary<string, string?> Describe(ServerDatabase database) => new()
    {
        ["database"] = database.Name,
        ["username"] = database.Username
    };
}
