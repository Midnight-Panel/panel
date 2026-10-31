using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.Shared.Http.Daemon;

namespace Midnight.ApiServer.Features.Nodes.Http;

/// <summary>Endpoints called by the daemons, authenticated with the node token</summary>
[ApiController]
[Route("api/nodes/remote")]
[Authorize(AuthenticationSchemes = NodeAuthenticationHandler.SchemeName)]
public class RemoteNodesController : ControllerBase
{
    private readonly NodeService _nodeService;
    private readonly ILogger<RemoteNodesController> _logger;

    public RemoteNodesController(NodeService nodeService, ILogger<RemoteNodesController> logger)
    {
        _nodeService = nodeService;
        _logger = logger;
    }

    /// <summary>Called by the daemon on start to sync its containers with the panel</summary>
    [HttpGet("boot")]
    public async Task<BootResponse> Boot(CancellationToken cancellationToken)
    {
        var node = await _nodeService.GetAsync(int.Parse(User.FindFirst(NodeAuthenticationHandler.NodeIdClaim)!.Value), cancellationToken);

        _logger.LogInformation("Node {NodeId} ({NodeName}) booted from {Ip}", node.Id, node.Name, HttpContext.Connection.RemoteIpAddress);

        // TODO: Phase 3 - return the servers of this node
        return new BootResponse { NodeName = node.Name };
    }
}
