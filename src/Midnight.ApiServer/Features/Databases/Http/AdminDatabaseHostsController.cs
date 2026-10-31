using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Databases.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.Databases.Http;

[ApiController]
[Route("api/admin/database-hosts")]
public class AdminDatabaseHostsController : ControllerBase
{
    private readonly DatabaseHostService _hosts;
    private readonly ILogger<AdminDatabaseHostsController> _logger;

    public AdminDatabaseHostsController(DatabaseHostService hosts, ILogger<AdminDatabaseHostsController> logger)
    {
        _hosts = hosts;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminServersRead)]
    public Task<List<DatabaseHostResponse>> List(CancellationToken cancellationToken)
        => _hosts.ListAsync(cancellationToken);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.AdminServersRead)]
    public Task<DatabaseHostResponse> Get(int id, CancellationToken cancellationToken)
        => _hosts.GetResponseAsync(id, cancellationToken);

    /// <summary>Password required (400), unique name (409)</summary>
    [HttpPost]
    [RequirePermission(Permissions.AdminServersWrite)]
    [ProducesResponseType<DatabaseHostResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] DatabaseHostRequest request, CancellationToken cancellationToken)
    {
        var host = await _hosts.CreateAsync(request, cancellationToken);
        _logger.LogInformation("User {ActorId} created database host {HostId}", User.GetUserId(), host.Id);

        return Created($"/api/admin/database-hosts/{host.Id}", host);
    }

    /// <summary>Password null or empty keeps the stored one</summary>
    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AdminServersWrite)]
    public async Task<DatabaseHostResponse> Update(int id, [FromBody] DatabaseHostRequest request, CancellationToken cancellationToken)
    {
        var host = await _hosts.UpdateAsync(id, request, cancellationToken);
        _logger.LogInformation("User {ActorId} updated database host {HostId}", User.GetUserId(), id);
        return host;
    }

    /// <summary>409 while databases exist on the host</summary>
    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminServersWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _hosts.DeleteAsync(id, cancellationToken);
        _logger.LogWarning("User {ActorId} deleted database host {HostId}", User.GetUserId(), id);
        return NoContent();
    }

    /// <summary>Connects with the stored credentials (5 s timeout). Connection errors are reported in the body, not as status code.</summary>
    [HttpPost("{id:int}/test")]
    [RequirePermission(Permissions.AdminServersWrite)]
    public Task<DatabaseHostTestResponse> Test(int id, CancellationToken cancellationToken)
        => _hosts.TestAsync(id, cancellationToken);
}
