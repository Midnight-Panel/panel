using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Http;

[ApiController]
[Route("api/servers/{uuid:guid}/users")]
[RequirePermission(Permissions.AccountRead)]
public class ServerSubusersController : ControllerBase
{
    private readonly ServerAccessService _access;
    private readonly SubuserService _subusers;
    private readonly ActivityService _activity;

    public ServerSubusersController(ServerAccessService access, SubuserService subusers, ActivityService activity)
    {
        _access = access;
        _subusers = subusers;
        _activity = activity;
    }

    [HttpGet]
    public async Task<List<SubuserResponse>> List(Guid uuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.UserRead, cancellationToken);
        return await _subusers.ListAsync(access.Server, cancellationToken);
    }

    /// <summary>Email of an existing account (400 otherwise), not the owner (400), not already a subuser (409). Only own permissions can be granted (403).</summary>
    [HttpPost]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<SubuserResponse> Create(Guid uuid, [FromBody] CreateSubuserRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.UserCreate, cancellationToken);
        var subuser = await _subusers.CreateAsync(access, request, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.SubuserCreate, Describe(subuser.User.Username, subuser.GetPermissions()));

        return SubuserService.ToResponse(subuser);
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<SubuserResponse> Update(Guid uuid, int id, [FromBody] UpdateSubuserRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.UserUpdate, cancellationToken);
        var subuser = await _subusers.UpdateAsync(access, id, request, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.SubuserUpdate, Describe(subuser.User.Username, subuser.GetPermissions()));

        return SubuserService.ToResponse(subuser);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Delete(Guid uuid, int id, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.UserDelete, cancellationToken);
        var subuser = await _subusers.DeleteAsync(access, id, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.SubuserDelete, new Dictionary<string, string?> { ["user"] = subuser.User.Username });

        return NoContent();
    }

    private static Dictionary<string, string?> Describe(string username, List<string> permissions) => new()
    {
        ["user"] = username,
        ["permissions"] = permissions.Count.ToString()
    };
}
