using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Http;

[ApiController]
[Route("api/servers/{uuid:guid}/allocations")]
[RequirePermission(Permissions.AccountRead)]
public class ServerAllocationsController : ControllerBase
{
    private readonly ServerAccessService _access;
    private readonly ServerNetworkService _network;
    private readonly ActivityService _activity;

    public ServerAllocationsController(ServerAccessService access, ServerNetworkService network, ActivityService activity)
    {
        _access = access;
        _network = network;
        _activity = activity;
    }

    [HttpGet]
    public async Task<ServerAllocationListResponse> List(Guid uuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.AllocationRead, cancellationToken);
        return ServerNetworkService.ToList(access.Server);
    }

    /// <summary>Assigns a random free allocation of the node. 409 at the limit or when none is free, 502 when the node fails.</summary>
    [HttpPost]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ServerAllocationDto> Create(Guid uuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.AllocationCreate, cancellationToken);
        var allocation = await _network.AssignAsync(access.Server, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.AllocationCreate, Describe(allocation));

        return ServerNetworkService.ToDto(access.Server, allocation);
    }

    [HttpPut("{id:int}/notes")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ServerAllocationDto> UpdateNotes(Guid uuid, int id, [FromBody] UpdateAllocationNotesRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.AllocationUpdate, cancellationToken);
        var allocation = await _network.UpdateNotesAsync(access.Server, id, request.Notes, cancellationToken);

        var properties = Describe(allocation);
        properties["notes"] = allocation.Notes;
        await _activity.LogAsync(access.Server.Id, ActivityEvents.AllocationNotes, properties);

        return ServerNetworkService.ToDto(access.Server, allocation);
    }

    [HttpPost("{id:int}/primary")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ServerAllocationDto> SetPrimary(Guid uuid, int id, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.AllocationUpdate, cancellationToken);
        var allocation = await _network.SetPrimaryAsync(access.Server, id, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.AllocationPrimary, Describe(allocation));

        return ServerNetworkService.ToDto(access.Server, allocation);
    }

    /// <summary>409 for the primary allocation</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Delete(Guid uuid, int id, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.AllocationDelete, cancellationToken);
        var allocation = await _network.RemoveAsync(access.Server, id, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.AllocationDelete, Describe(allocation));

        return NoContent();
    }

    private static Dictionary<string, string?> Describe(Allocation allocation) => new()
    {
        ["allocation"] = ServerConfigurationBuilder.FormatAddress(allocation.IpAddress, allocation.Port)
    };
}
