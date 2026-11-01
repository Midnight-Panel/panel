using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Application;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Application.Http;

/// <summary>Servers. Changes go through the same service as the admin pages (502 and nothing saved when the node fails).</summary>
[Route(RoutePrefix + "/servers")]
public class ApplicationServersController : ApplicationControllerBase
{
    private readonly ServerService _servers;
    private readonly ActivityService _activity;
    private readonly ILogger<ApplicationServersController> _logger;

    public ApplicationServersController(ServerService servers, ActivityService activity, ILogger<ApplicationServersController> logger)
    {
        _servers = servers;
        _activity = activity;
        _logger = logger;
    }

    /// <summary>search = like the admin list, filter[name] = name contains, the other filters are exact matches</summary>
    [HttpGet]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.Read)]
    public Task<PagedResult<AdminServerSummaryResponse>> List(
        [FromQuery] PagedQuery query,
        [FromQuery(Name = "filter[name]")] string? name,
        [FromQuery(Name = "filter[uuid]")] Guid? uuid,
        [FromQuery(Name = "filter[externalId]")] string? externalId,
        [FromQuery(Name = "filter[ownerId]")] int? ownerId,
        [FromQuery(Name = "filter[nodeId]")] int? nodeId,
        CancellationToken cancellationToken)
        => _servers.ListAsync(query, new ServerListFilter(name, uuid, externalId, ownerId, nodeId), cancellationToken);

    [HttpGet("{id:int}")]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.Read)]
    public async Task<AdminServerDetailResponse> Get(int id, CancellationToken cancellationToken)
        => ServerService.ToAdminDetail(await _servers.GetAsync(id, cancellationToken));

    [HttpGet("external/{externalId}")]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.Read)]
    public async Task<AdminServerDetailResponse> GetByExternalId(string externalId, CancellationToken cancellationToken)
        => ServerService.ToAdminDetail(await _servers.GetByExternalIdAsync(externalId, cancellationToken));

    /// <summary>
    /// Creates the server and sends it to the demon. Either NodeId + PrimaryAllocationId or Deploy, which picks
    /// free allocations itself (409 when no node has a matching free allocation).
    /// </summary>
    [HttpPost]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.ReadWrite)]
    [ProducesResponseType<AdminServerDetailResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ApplicationCreateServerRequest request, [FromServices] AllocationService allocations, CancellationToken cancellationToken)
    {
        int nodeId;
        int primaryAllocationId;
        List<int> additionalAllocationIds;

        if (request.Deploy != null)
        {
            if (request.PrimaryAllocationId.HasValue || request.AdditionalAllocationIds.Count > 0)
                throw InvalidRequest("Deploy", "Use either deploy or primaryAllocationId/additionalAllocationIds");

            var nodeIds = request.Deploy.NodeIds.ToList();

            if (request.Deploy.NodeId.HasValue)
                nodeIds.Add(request.Deploy.NodeId.Value);

            var ports = request.Deploy.Ports.SelectMany(AllocationService.ParsePortRanges).ToList();
            var picked = await allocations.FindDeployAllocationsAsync(nodeIds, ports, request.Deploy.AdditionalAllocations, cancellationToken);

            (nodeId, primaryAllocationId, additionalAllocationIds) = (picked.NodeId, picked.PrimaryAllocationId, picked.AdditionalAllocationIds);
        }
        else
        {
            if (!request.PrimaryAllocationId.HasValue)
                throw InvalidRequest("PrimaryAllocationId", "Set primaryAllocationId or deploy");

            if (!request.NodeId.HasValue)
                throw InvalidRequest("NodeId", "nodeId is required together with primaryAllocationId");

            (nodeId, primaryAllocationId, additionalAllocationIds) = (request.NodeId.Value, request.PrimaryAllocationId.Value, request.AdditionalAllocationIds);
        }

        var create = new CreateServerRequest
        {
            Name = request.Name,
            Description = request.Description,
            OwnerId = request.OwnerId,
            NodeId = nodeId,
            ShardId = request.ShardId,
            DockerImage = request.DockerImage,
            StartupCommand = request.StartupCommand,
            Limits = request.Limits,
            FeatureLimits = request.FeatureLimits,
            PrimaryAllocationId = primaryAllocationId,
            AdditionalAllocationIds = additionalAllocationIds,
            Variables = request.Variables,
            StartOnCompletion = request.StartOnCompletion,
            SkipInstall = request.SkipInstall
        };

        var server = await _servers.CreateAsync(create, request.ExternalId, cancellationToken);

        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) created server {ServerId} ({ServerUuid}) on node {NodeId}",
            KeyIdentifier, CreatorId, server.Id, server.Uuid, server.NodeId);

        return Created($"/{RoutePrefix}/servers/{server.Id}", ServerService.ToAdminDetail(await _servers.GetAsync(server.Id, cancellationToken)));
    }

    [HttpPatch("{id:int}/details")]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.ReadWrite)]
    public async Task<AdminServerDetailResponse> UpdateDetails(int id, [FromBody] ApplicationUpdateServerDetailsRequest request, CancellationToken cancellationToken)
    {
        var current = await _servers.GetAsync(id, cancellationToken);

        var update = new UpdateServerDetailsRequest
        {
            Name = request.Name ?? current.Name,
            Description = request.Description ?? current.Description,
            OwnerId = request.OwnerId ?? current.OwnerId
        };

        var externalId = request.ExternalId == null ? null : new ExternalIdChange(request.ExternalId);
        var server = await _servers.UpdateDetailsAsync(id, update, externalId, cancellationToken);

        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) updated the details of server {ServerId}", KeyIdentifier, CreatorId, id);

        if (request.Name != null && request.Name.Trim() != current.Name)
            await LogActivityAsync(id, ActivityEvents.Rename, new Dictionary<string, string?> { ["name"] = server.Name });

        return ServerService.ToAdminDetail(server);
    }

    /// <summary>Limits are merged with the current ones, allocations only change when set</summary>
    [HttpPatch("{id:int}/build")]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.ReadWrite)]
    public async Task<AdminServerDetailResponse> UpdateBuild(int id, [FromBody] ApplicationUpdateServerBuildRequest request, CancellationToken cancellationToken)
    {
        var current = await _servers.GetAsync(id, cancellationToken);
        var limits = request.Limits;
        var featureLimits = request.FeatureLimits;

        if (current.PrimaryAllocationId == null && request.PrimaryAllocationId == null)
            throw InvalidRequest("PrimaryAllocationId", "The server has no primary allocation, set primaryAllocationId");

        var primaryAllocationId = request.PrimaryAllocationId ?? current.PrimaryAllocationId!.Value;

        var update = new UpdateServerBuildRequest
        {
            Limits = new ServerLimitsDto
            {
                Cpu = limits?.Cpu ?? current.Cpu,
                Memory = limits?.Memory ?? current.Memory,
                Swap = limits?.Swap ?? current.Swap,
                Disk = limits?.Disk ?? current.Disk,
                IoWeight = limits?.IoWeight ?? current.IoWeight,
                Threads = limits?.Threads ?? current.Threads,
                OomKillerDisabled = limits?.OomKillerDisabled ?? current.OomKillerDisabled
            },
            FeatureLimits = new ServerFeatureLimitsDto
            {
                Databases = featureLimits?.Databases ?? current.DatabaseLimit,
                Allocations = featureLimits?.Allocations ?? current.AllocationLimit,
                Backups = featureLimits?.Backups ?? current.BackupLimit
            },
            PrimaryAllocationId = primaryAllocationId,
            AdditionalAllocationIds = request.AdditionalAllocationIds
                                      ?? current.Allocations.Where(x => x.Id != primaryAllocationId).Select(x => x.Id).ToList()
        };

        var server = await _servers.UpdateBuildAsync(id, update, cancellationToken);

        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) updated the build of server {ServerId}", KeyIdentifier, CreatorId, id);

        return ServerService.ToAdminDetail(server);
    }

    [HttpPatch("{id:int}/startup")]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.ReadWrite)]
    public async Task<AdminServerDetailResponse> UpdateStartup(int id, [FromBody] ApplicationUpdateServerStartupRequest request, CancellationToken cancellationToken)
    {
        var current = await _servers.GetAsync(id, cancellationToken);
        var shardChanged = request.ShardId.HasValue && request.ShardId.Value != current.ShardId;

        var update = new UpdateServerStartupRequest
        {
            ShardId = request.ShardId ?? current.ShardId,
            // A new shard without image gets its default image (empty = default)
            DockerImage = request.DockerImage ?? (shardChanged ? "" : current.DockerImage),
            StartupCommand = request.StartupCommand ?? current.StartupCommand,
            Variables = request.Variables ?? []
        };

        var server = await _servers.UpdateStartupAsync(id, update, cancellationToken);

        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) updated the startup of server {ServerId}", KeyIdentifier, CreatorId, id);
        await LogActivityAsync(id, ActivityEvents.StartupUpdate);

        return ServerService.ToAdminDetail(server);
    }

    [HttpPost("{id:int}/suspend")]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.ReadWrite)]
    public async Task<IActionResult> Suspend(int id, CancellationToken cancellationToken)
    {
        await _servers.SetSuspendedAsync(id, true, cancellationToken);
        _logger.LogWarning("Application key {KeyIdentifier} (created by user {CreatorId}) suspended server {ServerId}", KeyIdentifier, CreatorId, id);
        return NoContent();
    }

    [HttpPost("{id:int}/unsuspend")]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.ReadWrite)]
    public async Task<IActionResult> Unsuspend(int id, CancellationToken cancellationToken)
    {
        await _servers.SetSuspendedAsync(id, false, cancellationToken);
        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) unsuspended server {ServerId}", KeyIdentifier, CreatorId, id);
        return NoContent();
    }

    [HttpPost("{id:int}/reinstall")]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.ReadWrite)]
    public async Task<IActionResult> Reinstall(int id, CancellationToken cancellationToken)
    {
        await _servers.ReinstallAsync(await _servers.GetAsync(id, cancellationToken), cancellationToken);
        _logger.LogInformation("Application key {KeyIdentifier} (created by user {CreatorId}) reinstalled server {ServerId}", KeyIdentifier, CreatorId, id);
        await LogActivityAsync(id, ActivityEvents.Reinstall);
        return NoContent();
    }

    /// <summary>force = delete in the panel even if the demon can't delete the container and files</summary>
    [HttpDelete("{id:int}")]
    [ApplicationAccess(ApplicationResource.Servers, ApplicationAccess.ReadWrite)]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        await _servers.DeleteAsync(id, force, cancellationToken);
        _logger.LogWarning("Application key {KeyIdentifier} (created by user {CreatorId}) deleted server {ServerId} (force: {Force})", KeyIdentifier, CreatorId, id, force);
        return NoContent();
    }

    /// <summary>Attributed to the key's creator (user id claim) with the key as property</summary>
    private Task LogActivityAsync(int serverId, string eventName, Dictionary<string, string?>? properties = null)
    {
        properties ??= [];
        properties["applicationKey"] = KeyIdentifier;
        return _activity.LogAsync(serverId, eventName, properties);
    }

    private static HttpApiException InvalidRequest(string field, string message)
        => new(StatusCodes.Status400BadRequest, "Invalid server request", message) { Errors = new Dictionary<string, string> { [field] = message } };
}
