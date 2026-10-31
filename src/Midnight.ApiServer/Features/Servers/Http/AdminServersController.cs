using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Http;

[ApiController]
[Route("api/admin/servers")]
public class AdminServersController : ControllerBase
{
    private readonly ServerService _serverService;
    private readonly ILogger<AdminServersController> _logger;

    public AdminServersController(ServerService serverService, ILogger<AdminServersController> logger)
    {
        _serverService = serverService;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminServersRead)]
    public Task<PagedResult<AdminServerSummaryResponse>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => _serverService.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.AdminServersRead)]
    public async Task<AdminServerDetailResponse> Get(int id, CancellationToken cancellationToken)
        => ServerService.ToAdminDetail(await _serverService.GetAsync(id, cancellationToken));

    /// <summary>Creates the server and sends it to the demon. Nothing is saved when the node is not reachable (502).</summary>
    [HttpPost]
    [RequirePermission(Permissions.AdminServersWrite)]
    [ProducesResponseType<AdminServerDetailResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateServerRequest request, CancellationToken cancellationToken)
    {
        var server = await _serverService.CreateAsync(request, cancellationToken);
        _logger.LogInformation("User {ActorId} created server {ServerId} ({ServerUuid}) on node {NodeId}", User.GetUserId(), server.Id, server.Uuid, server.NodeId);

        return Created($"/api/admin/servers/{server.Id}", ServerService.ToAdminDetail(await _serverService.GetAsync(server.Id, cancellationToken)));
    }

    [HttpPut("{id:int}/details")]
    [RequirePermission(Permissions.AdminServersWrite)]
    public async Task<AdminServerDetailResponse> UpdateDetails(int id, [FromBody] UpdateServerDetailsRequest request, CancellationToken cancellationToken)
    {
        var server = await _serverService.UpdateDetailsAsync(id, request, cancellationToken);
        _logger.LogInformation("User {ActorId} updated the details of server {ServerId}", User.GetUserId(), id);
        return ServerService.ToAdminDetail(server);
    }

    [HttpPut("{id:int}/build")]
    [RequirePermission(Permissions.AdminServersWrite)]
    public async Task<AdminServerDetailResponse> UpdateBuild(int id, [FromBody] UpdateServerBuildRequest request, CancellationToken cancellationToken)
    {
        var server = await _serverService.UpdateBuildAsync(id, request, cancellationToken);
        _logger.LogInformation("User {ActorId} updated the build of server {ServerId}", User.GetUserId(), id);
        return ServerService.ToAdminDetail(server);
    }

    [HttpPut("{id:int}/startup")]
    [RequirePermission(Permissions.AdminServersWrite)]
    public async Task<AdminServerDetailResponse> UpdateStartup(int id, [FromBody] UpdateServerStartupRequest request, CancellationToken cancellationToken)
    {
        var server = await _serverService.UpdateStartupAsync(id, request, cancellationToken);
        _logger.LogInformation("User {ActorId} updated the startup of server {ServerId}", User.GetUserId(), id);
        return ServerService.ToAdminDetail(server);
    }

    [HttpPost("{id:int}/suspend")]
    [RequirePermission(Permissions.AdminServersWrite)]
    public async Task<IActionResult> Suspend(int id, CancellationToken cancellationToken)
    {
        await _serverService.SetSuspendedAsync(id, true, cancellationToken);
        _logger.LogWarning("User {ActorId} suspended server {ServerId}", User.GetUserId(), id);
        return NoContent();
    }

    [HttpPost("{id:int}/unsuspend")]
    [RequirePermission(Permissions.AdminServersWrite)]
    public async Task<IActionResult> Unsuspend(int id, CancellationToken cancellationToken)
    {
        await _serverService.SetSuspendedAsync(id, false, cancellationToken);
        _logger.LogInformation("User {ActorId} unsuspended server {ServerId}", User.GetUserId(), id);
        return NoContent();
    }

    [HttpPost("{id:int}/reinstall")]
    [RequirePermission(Permissions.AdminServersWrite)]
    public async Task<IActionResult> Reinstall(int id, CancellationToken cancellationToken)
    {
        await _serverService.ReinstallAsync(await _serverService.GetAsync(id, cancellationToken), cancellationToken);
        _logger.LogInformation("User {ActorId} reinstalled server {ServerId}", User.GetUserId(), id);
        return NoContent();
    }

    /// <summary>force = delete in the panel even if the demon can't delete the container and files</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminServersWrite)]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        await _serverService.DeleteAsync(id, force, cancellationToken);
        _logger.LogWarning("User {ActorId} deleted server {ServerId} (force: {Force})", User.GetUserId(), id, force);
        return NoContent();
    }
}
