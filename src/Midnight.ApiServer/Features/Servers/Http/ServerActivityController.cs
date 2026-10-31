using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Http;

[ApiController]
[Route("api/servers/{uuid:guid}/activity")]
[RequirePermission(Permissions.AccountRead)]
public class ServerActivityController : ControllerBase
{
    private readonly ServerAccessService _access;
    private readonly ActivityService _activity;

    public ServerActivityController(ServerAccessService access, ActivityService activity)
    {
        _access = access;
        _activity = activity;
    }

    /// <summary>Newest first, pageSize max 100</summary>
    [HttpGet]
    public async Task<PagedResult<ServerActivityResponse>> List(Guid uuid, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ActivityRead, cancellationToken);
        return await _activity.ListAsync(access.Server.Id, page, pageSize, cancellationToken);
    }
}
