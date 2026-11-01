using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Application;
using Midnight.Shared.Http.Nodes;

namespace Midnight.ApiServer.Features.Application.Http;

/// <summary>Nodes (resource Nodes) and their allocations (resource Allocations)</summary>
[Route(RoutePrefix + "/nodes")]
public class ApplicationNodesController : ApplicationControllerBase
{
    private readonly NodeService _nodes;
    private readonly AllocationService _allocations;
    private readonly ILogger<ApplicationNodesController> _logger;

    public ApplicationNodesController(NodeService nodes, AllocationService allocations, ILogger<ApplicationNodesController> logger)
    {
        _nodes = nodes;
        _allocations = allocations;
        _logger = logger;
    }

    [HttpGet]
    [ApplicationAccess(ApplicationResource.Nodes, ApplicationAccess.Read)]
    public Task<PagedResult<NodeResponse>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => _nodes.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    [ApplicationAccess(ApplicationResource.Nodes, ApplicationAccess.Read)]
    public Task<NodeResponse> Get(int id, CancellationToken cancellationToken)
        => _nodes.GetResponseAsync(id, cancellationToken);

    /// <summary>The demon configuration (/etc/midnight/config.json) of the setup tab. Contains the node token, needs ReadWrite.</summary>
    [HttpGet("{id:int}/configuration")]
    [ApplicationAccess(ApplicationResource.Nodes, ApplicationAccess.ReadWrite)]
    public async Task<NodeSetupResponse> Configuration(int id, CancellationToken cancellationToken)
        => _nodes.GetSetup(await _nodes.GetAsync(id, cancellationToken));

    [HttpPost]
    [ApplicationAccess(ApplicationResource.Nodes, ApplicationAccess.ReadWrite)]
    [ProducesResponseType<NodeResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] NodeRequest request, CancellationToken cancellationToken)
    {
        var node = await _nodes.CreateAsync(request, cancellationToken);
        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) created node {NodeId}", KeyIdentifier, CreatorId, node.Id);
        return Created($"/{RoutePrefix}/nodes/{node.Id}", node);
    }

    [HttpPatch("{id:int}")]
    [ApplicationAccess(ApplicationResource.Nodes, ApplicationAccess.ReadWrite)]
    public async Task<NodeResponse> Update(int id, [FromBody] ApplicationUpdateNodeRequest request, CancellationToken cancellationToken)
    {
        var current = await _nodes.GetAsync(id, cancellationToken);

        var node = await _nodes.UpdateAsync(id, new NodeRequest
        {
            Name = request.Name ?? current.Name,
            Fqdn = request.Fqdn ?? current.Fqdn,
            HttpPort = request.HttpPort ?? current.HttpPort,
            SftpPort = request.SftpPort ?? current.SftpPort,
            UseSsl = request.UseSsl ?? current.UseSsl
        }, cancellationToken);

        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) updated node {NodeId}", KeyIdentifier, CreatorId, id);
        return node;
    }

    /// <summary>409 while servers are on the node</summary>
    [HttpDelete("{id:int}")]
    [ApplicationAccess(ApplicationResource.Nodes, ApplicationAccess.ReadWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _nodes.DeleteAsync(id, cancellationToken);
        _logger.LogWarning("Application key {KeyIdentifier} (created by user {CreatorId}) deleted node {NodeId}", KeyIdentifier, CreatorId, id);
        return NoContent();
    }

    // ─── Allocations ────────────────────────────────────────────────────────

    /// <summary>free=true: only allocations without server</summary>
    [HttpGet("{id:int}/allocations")]
    [ApplicationAccess(ApplicationResource.Allocations, ApplicationAccess.Read)]
    public async Task<PagedResult<AllocationResponse>> Allocations(int id, [FromQuery] PagedQuery query, [FromQuery] bool free = false, CancellationToken cancellationToken = default)
    {
        await _nodes.GetAsync(id, cancellationToken);
        return await _allocations.ListAsync(id, query, free, cancellationToken);
    }

    /// <summary>Ports like "25565, 25570-25600" (max 1000 per request), existing ones are skipped</summary>
    [HttpPost("{id:int}/allocations")]
    [ApplicationAccess(ApplicationResource.Allocations, ApplicationAccess.ReadWrite)]
    public async Task<CreateAllocationsResponse> CreateAllocations(int id, [FromBody] CreateAllocationsRequest request, CancellationToken cancellationToken)
    {
        var result = await _allocations.CreateAsync(id, request, cancellationToken);
        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) created {Count} allocation(s) on node {NodeId}", KeyIdentifier, CreatorId, result.Created, id);
        return result;
    }

    /// <summary>409 while the allocation is assigned to a server</summary>
    [HttpDelete("{id:int}/allocations/{allocationId:int}")]
    [ApplicationAccess(ApplicationResource.Allocations, ApplicationAccess.ReadWrite)]
    public async Task<IActionResult> DeleteAllocation(int id, int allocationId, CancellationToken cancellationToken)
    {
        if (await _allocations.DeleteAsync(id, [allocationId], cancellationToken) == 0)
            throw HttpApiException.NotFound("Allocation not found");

        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) deleted allocation {AllocationId} of node {NodeId}", KeyIdentifier, CreatorId, allocationId, id);
        return NoContent();
    }
}
