using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Demon;
using Midnight.Shared.Http.Nodes;

namespace Midnight.ApiServer.Features.Nodes.Http;

[ApiController]
[Route("api/admin/nodes")]
public class AdminNodesController : ControllerBase
{
    private readonly NodeService _nodeService;
    private readonly AllocationService _allocationService;
    private readonly NodeClient _nodeClient;
    private readonly ILogger<AdminNodesController> _logger;

    public AdminNodesController(NodeService nodeService, AllocationService allocationService, NodeClient nodeClient, ILogger<AdminNodesController> logger)
    {
        _nodeService = nodeService;
        _allocationService = allocationService;
        _nodeClient = nodeClient;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminNodesRead)]
    public Task<PagedResult<NodeResponse>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => _nodeService.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.AdminNodesRead)]
    public Task<NodeResponse> Get(int id, CancellationToken cancellationToken)
        => _nodeService.GetResponseAsync(id, cancellationToken);

    [HttpPost]
    [RequirePermission(Permissions.AdminNodesWrite)]
    public async Task<NodeResponse> Create([FromBody] NodeRequest request, CancellationToken cancellationToken)
    {
        var node = await _nodeService.CreateAsync(request, cancellationToken);
        _logger.LogInformation("User {ActorId} created node {NodeId}", User.GetUserId(), node.Id);
        return node;
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AdminNodesWrite)]
    public async Task<NodeResponse> Update(int id, [FromBody] NodeRequest request, CancellationToken cancellationToken)
    {
        var node = await _nodeService.UpdateAsync(id, request, cancellationToken);
        _logger.LogInformation("User {ActorId} updated node {NodeId}", User.GetUserId(), id);
        return node;
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminNodesWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _nodeService.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("User {ActorId} deleted node {NodeId}", User.GetUserId(), id);
        return NoContent();
    }

    /// <summary>Live status of the demon (system info, docker). Offline nodes return Online = false, not an error.</summary>
    [HttpGet("{id:int}/status")]
    [RequirePermission(Permissions.AdminNodesRead)]
    public async Task<NodeStatusResponse> Status(int id, CancellationToken cancellationToken)
        => await _nodeClient.GetStatusAsync(await _nodeService.GetAsync(id, cancellationToken), cancellationToken);

    [HttpGet("{id:int}/logs")]
    [RequirePermission(Permissions.AdminNodesRead)]
    public async Task<DemonLogsResponse> Logs(int id, [FromQuery] int lines = 200, CancellationToken cancellationToken = default)
    {
        var node = await _nodeService.GetAsync(id, cancellationToken);

        try
        {
            return await _nodeClient.GetLogsAsync(node, Math.Clamp(lines, 1, 1000), cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or NodeException && !cancellationToken.IsCancellationRequested)
        {
            throw new HttpApiException(StatusCodes.Status502BadGateway, "The demon is not reachable", e.Message);
        }
    }

    /// <summary>Demon configuration including the secret token. Needs write access.</summary>
    [HttpGet("{id:int}/setup")]
    [RequirePermission(Permissions.AdminNodesWrite)]
    public async Task<NodeSetupResponse> Setup(int id, CancellationToken cancellationToken)
        => _nodeService.GetSetup(await _nodeService.GetAsync(id, cancellationToken));

    [HttpPost("{id:int}/reset-token")]
    [RequirePermission(Permissions.AdminNodesWrite)]
    public async Task<IActionResult> ResetToken(int id, CancellationToken cancellationToken)
    {
        await _nodeService.ResetTokenAsync(id, cancellationToken);
        _logger.LogWarning("User {ActorId} reset the token of node {NodeId}", User.GetUserId(), id);
        return NoContent();
    }

    /// <summary>free=true: only allocations without server (for the server create form)</summary>
    [HttpGet("{id:int}/allocations")]
    [RequirePermission(Permissions.AdminNodesRead)]
    public Task<PagedResult<AllocationResponse>> Allocations(int id, [FromQuery] PagedQuery query, [FromQuery] bool free = false, CancellationToken cancellationToken = default)
        => _allocationService.ListAsync(id, query, free, cancellationToken);

    [HttpPost("{id:int}/allocations")]
    [RequirePermission(Permissions.AdminNodesWrite)]
    public Task<CreateAllocationsResponse> CreateAllocations(int id, [FromBody] CreateAllocationsRequest request, CancellationToken cancellationToken)
        => _allocationService.CreateAsync(id, request, cancellationToken);

    [HttpPost("{id:int}/allocations/delete")]
    [RequirePermission(Permissions.AdminNodesWrite)]
    public async Task<IActionResult> DeleteAllocations(int id, [FromBody] DeleteAllocationsRequest request, CancellationToken cancellationToken)
    {
        await _allocationService.DeleteAsync(id, request.Ids, cancellationToken);
        return NoContent();
    }
}
