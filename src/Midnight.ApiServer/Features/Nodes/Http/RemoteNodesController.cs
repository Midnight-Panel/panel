using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared.Http.Demon;

namespace Midnight.ApiServer.Features.Nodes.Http;

/// <summary>Endpoints called by the demons, authenticated with the node token</summary>
[ApiController]
[Route("api/nodes/remote")]
[Authorize(AuthenticationSchemes = NodeAuthenticationHandler.SchemeName)]
public class RemoteNodesController : ControllerBase
{
    private readonly NodeService _nodeService;
    private readonly ServerService _serverService;
    private readonly BackupService _backupService;
    private readonly ActivityService _activity;
    private readonly DataContext _context;
    private readonly ILogger<RemoteNodesController> _logger;

    public RemoteNodesController(NodeService nodeService, ServerService serverService, BackupService backupService, ActivityService activity,
        DataContext context, ILogger<RemoteNodesController> logger)
    {
        _nodeService = nodeService;
        _serverService = serverService;
        _backupService = backupService;
        _activity = activity;
        _context = context;
        _logger = logger;
    }

    private int NodeId => int.Parse(User.FindFirst(NodeAuthenticationHandler.NodeIdClaim)!.Value);

    /// <summary>Called by the demon on start to sync its containers with the panel</summary>
    [HttpGet("boot")]
    public async Task<BootResponse> Boot(CancellationToken cancellationToken)
    {
        var node = await _nodeService.GetAsync(NodeId, cancellationToken);

        _logger.LogInformation("Node {NodeId} ({NodeName}) booted from {Ip}", node.Id, node.Name, HttpContext.Connection.RemoteIpAddress);

        // The full configurations are fetched from GET /api/nodes/remote/servers
        var servers = await _context.Servers
            .AsNoTracking()
            .Where(x => x.NodeId == node.Id)
            .OrderBy(x => x.Id)
            .Select(x => new BootServer { Uuid = x.Uuid, Name = x.Name })
            .ToListAsync(cancellationToken);

        return new BootResponse { NodeName = node.Name, Servers = servers };
    }

    /// <summary>Configurations of all servers of the calling node, the demon recreates its state from them</summary>
    [HttpGet("servers")]
    public Task<List<DemonServerConfiguration>> Servers(CancellationToken cancellationToken)
        => _serverService.ListNodeConfigurationsAsync(NodeId, cancellationToken);

    /// <summary>Result of an installation. Only servers of the calling node can be reported (404 otherwise).</summary>
    [HttpPost("servers/{uuid:guid}/install")]
    public async Task<IActionResult> InstallResult(Guid uuid, [FromBody] DemonInstallResultRequest request, CancellationToken cancellationToken)
    {
        await _serverService.SetInstallResultAsync(NodeId, uuid, request.Successful, cancellationToken);

        if (request.Successful)
            _logger.LogInformation("Server {ServerUuid} was installed on node {NodeId}", uuid, NodeId);
        else
            _logger.LogWarning("Installation of server {ServerUuid} failed on node {NodeId}", uuid, NodeId);

        return NoContent();
    }

    /// <summary>Result of a backup. Only backups of servers on the calling node can be reported (404 otherwise).</summary>
    [HttpPost("backups/{backupUuid:guid}")]
    public async Task<IActionResult> BackupResult(Guid backupUuid, [FromBody] DemonBackupResultRequest request, CancellationToken cancellationToken)
    {
        var backup = await _backupService.SetResultAsync(NodeId, backupUuid, request, cancellationToken);

        await _activity.LogAsync(backup.ServerId, request.Successful ? ActivityEvents.BackupComplete : ActivityEvents.BackupFail,
            new Dictionary<string, string?> { ["backup"] = backup.Name, ["uuid"] = backup.Uuid.ToString() }, ActivityActor.System);

        if (request.Successful)
            _logger.LogInformation("Backup {BackupUuid} of server {ServerId} completed ({Bytes} bytes)", backupUuid, backup.ServerId, request.Bytes);
        else
            _logger.LogWarning("Backup {BackupUuid} of server {ServerId} failed on node {NodeId}", backupUuid, backup.ServerId, NodeId);

        return NoContent();
    }

    /// <summary>Result of a restore. Only backups of servers on the calling node (404 otherwise).</summary>
    [HttpPost("backups/{backupUuid:guid}/restore")]
    public async Task<IActionResult> RestoreResult(Guid backupUuid, [FromBody] DemonRestoreResultRequest request, CancellationToken cancellationToken)
    {
        var backup = await _backupService.GetForNodeAsync(NodeId, backupUuid, cancellationToken);

        await _activity.LogAsync(backup.ServerId, request.Successful ? ActivityEvents.BackupRestoreComplete : ActivityEvents.BackupRestoreFail,
            new Dictionary<string, string?> { ["backup"] = backup.Name, ["uuid"] = backup.Uuid.ToString() }, ActivityActor.System);

        if (!request.Successful)
            _logger.LogWarning("Restoring backup {BackupUuid} of server {ServerId} failed on node {NodeId}", backupUuid, backup.ServerId, NodeId);

        return NoContent();
    }
}
