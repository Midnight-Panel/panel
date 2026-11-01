using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Databases.Services;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.ApiServer.Features.Shards.Services;
using Midnight.Shared;
using Midnight.Shared.Enums;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Demon;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>Exact filters of the admin server list (application api), combined with AND. Name is a contains search.</summary>
public record ServerListFilter(string? Name = null, Guid? Uuid = null, string? ExternalId = null, int? OwnerId = null, int? NodeId = null);

/// <summary>
/// Creates and changes servers. The panel is the source of truth: changes that affect the container are saved in a
/// transaction that is only committed once the demon accepted the new configuration (502 and nothing saved otherwise).
/// </summary>
public class ServerService
{
    private readonly DataContext _context;
    private readonly NodeClient _nodeClient;
    private readonly IOptionsMonitor<AppConfiguration> _configuration;
    private readonly ServerDatabaseService _databaseService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServerService> _logger;

    public ServerService(DataContext context, NodeClient nodeClient, IOptionsMonitor<AppConfiguration> configuration, ServerDatabaseService databaseService,
        TimeProvider timeProvider, ILogger<ServerService> logger)
    {
        _context = context;
        _nodeClient = nodeClient;
        _configuration = configuration;
        _databaseService = databaseService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ─── Queries ────────────────────────────────────────────────────────────

    /// <summary>Servers with everything the responses and the demon configuration need</summary>
    private IQueryable<Server> Detailed() => _context.Servers
        .Include(x => x.Owner)
        .Include(x => x.Node)
        .Include(x => x.Shard).ThenInclude(x => x.Variables)
        .Include(x => x.Variables)
        .Include(x => x.Allocations)
        .AsSplitQuery();

    public async Task<Server> GetAsync(int id, CancellationToken cancellationToken)
        => await Detailed().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
           ?? throw HttpApiException.NotFound("Server not found");

    public async Task<Server> GetAsync(Guid uuid, CancellationToken cancellationToken)
        => await Detailed().FirstOrDefaultAsync(x => x.Uuid == uuid, cancellationToken)
           ?? throw HttpApiException.NotFound("Server not found");

    public async Task<Server> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken)
    {
        var normalized = ExternalIds.Normalize(externalId) ?? throw HttpApiException.NotFound("Server not found");

        return await Detailed().FirstOrDefaultAsync(x => x.ExternalId == normalized, cancellationToken)
               ?? throw HttpApiException.NotFound("Server not found");
    }

    public Task<PagedResult<AdminServerSummaryResponse>> ListAsync(PagedQuery query, CancellationToken cancellationToken)
        => ListAsync(query, null, cancellationToken);

    public async Task<PagedResult<AdminServerSummaryResponse>> ListAsync(PagedQuery query, ServerListFilter? filter, CancellationToken cancellationToken)
    {
        var servers = _context.Servers.AsNoTracking();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Name))
            {
                var name = filter.Name.Trim().ToLower();
                servers = servers.Where(x => x.Name.ToLower().Contains(name));
            }

            if (filter.Uuid.HasValue)
                servers = servers.Where(x => x.Uuid == filter.Uuid.Value);

            if (!string.IsNullOrWhiteSpace(filter.ExternalId))
            {
                var externalId = filter.ExternalId.Trim();
                servers = servers.Where(x => x.ExternalId == externalId);
            }

            if (filter.OwnerId.HasValue)
                servers = servers.Where(x => x.OwnerId == filter.OwnerId.Value);

            if (filter.NodeId.HasValue)
                servers = servers.Where(x => x.NodeId == filter.NodeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            if (Guid.TryParse(search, out var uuid))
            {
                servers = servers.Where(x => x.Uuid == uuid);
            }
            else
            {
                var lower = search.ToLower();
                servers = servers.Where(x => x.Name.ToLower().Contains(lower) ||
                                             x.Owner.Username.ToLower().Contains(lower) ||
                                             x.Owner.Email.Contains(lower) ||
                                             x.Node.Name.ToLower().Contains(lower) ||
                                             x.Shard.Name.ToLower().Contains(lower));
            }
        }

        servers = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", false) => servers.OrderBy(x => x.Name),
            ("name", true) => servers.OrderByDescending(x => x.Name),
            ("owner", false) => servers.OrderBy(x => x.Owner.Username),
            ("owner", true) => servers.OrderByDescending(x => x.Owner.Username),
            ("node", false) => servers.OrderBy(x => x.Node.Name).ThenBy(x => x.Name),
            ("node", true) => servers.OrderByDescending(x => x.Node.Name).ThenBy(x => x.Name),
            ("shard", false) => servers.OrderBy(x => x.Shard.Name).ThenBy(x => x.Name),
            ("shard", true) => servers.OrderByDescending(x => x.Shard.Name).ThenBy(x => x.Name),
            ("status", false) => servers.OrderBy(x => x.Status).ThenBy(x => x.Name),
            ("status", true) => servers.OrderByDescending(x => x.Status).ThenBy(x => x.Name),
            ("createdat", false) => servers.OrderBy(x => x.CreatedAt),
            ("createdat", true) => servers.OrderByDescending(x => x.CreatedAt),
            (_, true) => servers.OrderByDescending(x => x.Id),
            _ => servers.OrderBy(x => x.Id)
        };

        var total = await servers.CountAsync(cancellationToken);

        var page = await servers
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Include(x => x.Owner)
            .Include(x => x.Node)
            .Include(x => x.Shard)
            .Include(x => x.PrimaryAllocation)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminServerSummaryResponse>
        {
            Items = page.Select(x => FillAdminSummary(x, new AdminServerSummaryResponse())).ToList(),
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            TotalCount = total
        };
    }

    /// <summary>Owned servers and servers the user is a subuser of, or every server when all is set (the caller checks the permission)</summary>
    public async Task<List<Server>> ListForUserAsync(int userId, bool all, CancellationToken cancellationToken)
    {
        var servers = _context.Servers.AsNoTracking();

        if (!all)
            servers = servers.Where(x => x.OwnerId == userId || x.Subusers.Any(s => s.UserId == userId));

        return await servers
            .Include(x => x.Node)
            .Include(x => x.Shard)
            .Include(x => x.PrimaryAllocation)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    // ─── Admin: create and update ───────────────────────────────────────────

    public Task<Server> CreateAsync(CreateServerRequest request, CancellationToken cancellationToken)
        => CreateAsync(request, null, cancellationToken);

    public async Task<Server> CreateAsync(CreateServerRequest request, string? externalId, CancellationToken cancellationToken)
    {
        externalId = ExternalIds.Normalize(externalId);
        await EnsureExternalIdAvailableAsync(externalId, null, cancellationToken);

        if (!await _context.Users.AnyAsync(x => x.Id == request.OwnerId, cancellationToken))
            throw HttpApiException.BadRequest("Owner not found", "The selected user does not exist");

        var node = await _context.Nodes.FirstOrDefaultAsync(x => x.Id == request.NodeId, cancellationToken)
                   ?? throw HttpApiException.BadRequest("Node not found", "The selected node does not exist");

        var shard = await LoadShardAsync(request.ShardId, cancellationToken);
        var dockerImage = ResolveDockerImage(shard, request.DockerImage);
        var startupCommand = ValidateStartupCommand(shard, request.StartupCommand);

        var allocations = await LoadAllocationsAsync(node.Id, null, request.PrimaryAllocationId, request.AdditionalAllocationIds, cancellationToken);
        var values = ResolveVariables(shard, request.Variables, null);

        var now = _timeProvider.GetUtcNow();

        var server = new Server
        {
            Uuid = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = NullIfEmpty(request.Description),
            ExternalId = externalId,
            OwnerId = request.OwnerId,
            Node = node,
            Shard = shard,
            DockerImage = dockerImage,
            StartupCommand = startupCommand,
            Status = request.SkipInstall ? ServerStatus.Ready : ServerStatus.Installing,
            InstalledAt = request.SkipInstall ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        ApplyLimits(server, request.Limits, request.FeatureLimits);
        ApplyVariables(server, shard, values);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Two steps: the server references its primary allocation and the allocations reference the server
        _context.Servers.Add(server);
        await _context.SaveChangesAsync(cancellationToken);

        AssignAllocations(server, allocations, request.PrimaryAllocationId);
        await _context.SaveChangesAsync(cancellationToken);

        await CallNodeAsync(() => _nodeClient.CreateServerAsync(node, new DemonCreateServerRequest
        {
            Configuration = BuildConfiguration(server),
            SkipInstall = request.SkipInstall,
            StartOnCompletion = request.StartOnCompletion
        }, cancellationToken), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return server;
    }

    public Task<Server> UpdateDetailsAsync(int id, UpdateServerDetailsRequest request, CancellationToken cancellationToken)
        => UpdateDetailsAsync(id, request, null, cancellationToken);

    /// <param name="externalId">null = keep the current external id</param>
    public async Task<Server> UpdateDetailsAsync(int id, UpdateServerDetailsRequest request, ExternalIdChange? externalId, CancellationToken cancellationToken)
    {
        var server = await GetAsync(id, cancellationToken);

        if (externalId != null)
        {
            var normalized = ExternalIds.Normalize(externalId.Value);
            await EnsureExternalIdAvailableAsync(normalized, server.Id, cancellationToken);
            server.ExternalId = normalized;
        }

        if (server.OwnerId != request.OwnerId)
        {
            server.Owner = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.OwnerId, cancellationToken)
                           ?? throw HttpApiException.BadRequest("Owner not found", "The selected user does not exist");
        }

        server.Name = request.Name.Trim();
        server.Description = NullIfEmpty(request.Description);
        server.UpdatedAt = _timeProvider.GetUtcNow();

        // The name is only used for log messages on the demon, it gets it with the next configuration push or boot
        await _context.SaveChangesAsync(cancellationToken);

        return server;
    }

    public async Task<Server> UpdateBuildAsync(int id, UpdateServerBuildRequest request, CancellationToken cancellationToken)
    {
        var server = await GetAsync(id, cancellationToken);
        var allocations = await LoadAllocationsAsync(server.NodeId, server.Id, request.PrimaryAllocationId, request.AdditionalAllocationIds, cancellationToken);

        ApplyLimits(server, request.Limits, request.FeatureLimits);
        AssignAllocations(server, allocations, request.PrimaryAllocationId);
        server.UpdatedAt = _timeProvider.GetUtcNow();

        await SaveAndPushAsync(server, cancellationToken);

        return server;
    }

    public async Task<Server> UpdateStartupAsync(int id, UpdateServerStartupRequest request, CancellationToken cancellationToken)
    {
        var server = await GetAsync(id, cancellationToken);
        var shard = server.ShardId == request.ShardId ? server.Shard : await LoadShardAsync(request.ShardId, cancellationToken);

        var dockerImage = ResolveDockerImage(shard, request.DockerImage);
        var startupCommand = ValidateStartupCommand(shard, request.StartupCommand);

        // Values of the current shard are kept for variables with the same name
        var values = ResolveVariables(shard, request.Variables, ServerConfigurationBuilder.GetVariableValues(server, viewableOnly: false));

        server.Shard = shard;
        server.ShardId = shard.Id;
        server.DockerImage = dockerImage;
        server.StartupCommand = startupCommand;
        server.UpdatedAt = _timeProvider.GetUtcNow();

        ApplyVariables(server, shard, values);

        await SaveAndPushAsync(server, cancellationToken);

        return server;
    }

    /// <summary>Suspending also kills the container. The panel state changes even when the node is offline, the demon gets it on boot.</summary>
    public async Task SetSuspendedAsync(int id, bool suspended, CancellationToken cancellationToken)
    {
        var server = await GetAsync(id, cancellationToken);

        if (server.Suspended == suspended)
            return;

        server.Suspended = suspended;
        server.UpdatedAt = _timeProvider.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _nodeClient.UpdateServerAsync(server.Node, BuildConfiguration(server), cancellationToken);

            if (suspended)
                await _nodeClient.PowerAsync(server.Node, server.Uuid, PowerAction.Kill, cancellationToken);
        }
        catch (Exception e) when (NodeClient.IsNodeError(e, cancellationToken))
        {
            // A 409 means the container wasn't running, nothing to kill
            if (e is not NodeException { StatusCode: StatusCodes.Status409Conflict })
                _logger.LogWarning("Could not update server {ServerUuid} on node {NodeId} after (un)suspending: {Error}", server.Uuid, server.NodeId, e.Message);
        }
    }

    public async Task ReinstallAsync(Server server, CancellationToken cancellationToken)
    {
        if (server.Suspended)
            throw HttpApiException.Conflict("The server is suspended");

        if (server.Status == ServerStatus.Installing)
            throw HttpApiException.Conflict("The server is already being installed");

        server.Status = ServerStatus.Installing;
        server.UpdatedAt = _timeProvider.GetUtcNow();

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await CallNodeAsync(() => _nodeClient.ReinstallServerAsync(server.Node, server.Uuid, cancellationToken), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Deletes the server on the demon and in the panel. With force the panel entry is deleted even if the demon fails.</summary>
    public async Task DeleteAsync(int id, bool force, CancellationToken cancellationToken)
    {
        var server = await GetAsync(id, cancellationToken);

        try
        {
            await _nodeClient.DeleteServerAsync(server.Node, server.Uuid, cancellationToken);
        }
        catch (Exception e) when (NodeClient.IsNodeError(e, cancellationToken))
        {
            if (!force)
                throw NodeClient.ToApiException(e);

            _logger.LogWarning("Deleting server {ServerUuid} without removing it from node {NodeId}: {Error}", server.Uuid, server.NodeId, e.Message);
        }

        // The demon deleted the backup archives with the server. Databases live on the database hosts: best effort, failures are logged.
        await _databaseService.DropAllAsync(server.Id, cancellationToken);

        // Frees the allocations (ServerId is set to null), subusers, backups, schedules, databases and activities are deleted by cascade
        _context.Servers.Remove(server);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // ─── User actions ───────────────────────────────────────────────────────

    public async Task RenameAsync(Server server, RenameServerRequest request, CancellationToken cancellationToken)
    {
        server.Name = request.Name.Trim();
        server.Description = NullIfEmpty(request.Description);
        server.UpdatedAt = _timeProvider.GetUtcNow();

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Changes variables the user may edit, every other variable in the request is rejected</summary>
    public async Task UpdateUserVariablesAsync(Server server, UpdateServerVariablesRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();
        var values = ServerConfigurationBuilder.GetVariableValues(server, viewableOnly: false);

        foreach (var (key, value) in request.Variables)
        {
            var variable = server.Shard.Variables.FirstOrDefault(x => x.EnvVariable == key);

            if (variable is not { UserViewable: true, UserEditable: true })
            {
                errors[key] = $"'{key}' is unknown or can't be edited";
                continue;
            }

            values[variable.EnvVariable] = value?.Trim() ?? "";
        }

        if (errors.Count == 0)
        {
            foreach (var variable in server.Shard.Variables.Where(x => request.Variables.ContainsKey(x.EnvVariable)))
            {
                var (isValid, error) = ShardVariableValidator.ValidateValue(values[variable.EnvVariable], variable.Rules, variable.Name);

                if (!isValid)
                    errors[variable.EnvVariable] = error ?? "Invalid value";
            }
        }

        ThrowIfInvalid(errors);

        ApplyVariables(server, server.Shard, values);
        server.UpdatedAt = _timeProvider.GetUtcNow();

        await SaveAndPushAsync(server, cancellationToken);
    }

    public async Task PowerAsync(Server server, PowerAction action, CancellationToken cancellationToken)
    {
        EnsureUsable(server);

        if (action is PowerAction.Start or PowerAction.Restart && server.Status == ServerStatus.InstallFailed)
            throw HttpApiException.Conflict("The installation failed", "Reinstall the server before starting it");

        await CallNodeAsync(() => _nodeClient.PowerAsync(server.Node, server.Uuid, action, cancellationToken), cancellationToken);
    }

    public async Task SendCommandAsync(Server server, string command, CancellationToken cancellationToken)
    {
        EnsureUsable(server);

        await CallNodeAsync(() => _nodeClient.SendCommandAsync(server.Node, server.Uuid, command, cancellationToken), cancellationToken);
    }

    /// <summary>Live state of one server, NodeOnline = false when the demon can't be reached</summary>
    public async Task<ServerResourcesResponse> GetResourcesAsync(Server server, CancellationToken cancellationToken)
    {
        try
        {
            var state = await _nodeClient.GetServerAsync(server.Node, server.Uuid, cancellationToken);
            return new ServerResourcesResponse { State = state.State, Stats = state.Stats };
        }
        catch (Exception e) when (NodeClient.IsNodeError(e, cancellationToken))
        {
            if (e is NodeException { StatusCode: StatusCodes.Status404NotFound })
                return new ServerResourcesResponse { State = ServerState.Offline };

            _logger.LogDebug("Node {NodeId} is not reachable: {Error}", server.NodeId, e.Message);
            return new ServerResourcesResponse { State = ServerState.Offline, NodeOnline = false };
        }
    }

    /// <summary>Live state of many servers with one request per node</summary>
    public async Task<Dictionary<Guid, ServerResourcesResponse>> GetResourcesAsync(IReadOnlyCollection<Server> servers, CancellationToken cancellationToken)
    {
        var tasks = servers
            .GroupBy(x => x.NodeId)
            .Select(async group =>
            {
                var node = group.First().Node;
                Dictionary<Guid, DemonServerStateResponse>? states = null;

                try
                {
                    states = (await _nodeClient.ListServersAsync(node, cancellationToken))
                        .GroupBy(x => x.Uuid)
                        .ToDictionary(x => x.Key, x => x.First());
                }
                catch (Exception e) when (NodeClient.IsNodeError(e, cancellationToken))
                {
                    _logger.LogDebug("Node {NodeId} is not reachable: {Error}", node.Id, e.Message);
                }

                return group.Select(server => (server.Uuid, Resources: states == null
                    ? new ServerResourcesResponse { State = ServerState.Offline, NodeOnline = false }
                    : states.TryGetValue(server.Uuid, out var state)
                        ? new ServerResourcesResponse { State = state.State, Stats = state.Stats }
                        : new ServerResourcesResponse { State = ServerState.Offline }));
            });

        var results = await Task.WhenAll(tasks);

        return results.SelectMany(x => x).ToDictionary(x => x.Uuid, x => x.Resources);
    }

    // ─── Demon ──────────────────────────────────────────────────────────────

    public DemonServerConfiguration BuildConfiguration(Server server)
        => ServerConfigurationBuilder.Build(server, _configuration.CurrentValue.TimeZone);

    /// <summary>Configurations of all servers of a node, the demon recreates its state from them on boot</summary>
    public async Task<List<DemonServerConfiguration>> ListNodeConfigurationsAsync(int nodeId, CancellationToken cancellationToken)
    {
        var servers = await Detailed().AsNoTracking().Where(x => x.NodeId == nodeId).OrderBy(x => x.Id).ToListAsync(cancellationToken);

        var configurations = new List<DemonServerConfiguration>();

        foreach (var server in servers)
        {
            if (ServerConfigurationBuilder.GetPrimaryAllocation(server) == null)
            {
                _logger.LogWarning("Server {ServerUuid} has no primary allocation and is not sent to node {NodeId}", server.Uuid, nodeId);
                continue;
            }

            configurations.Add(BuildConfiguration(server));
        }

        return configurations;
    }

    /// <summary>Reported by the demon of the node the server is on, servers of other nodes are not found</summary>
    public async Task SetInstallResultAsync(int nodeId, Guid uuid, bool successful, CancellationToken cancellationToken)
    {
        var server = await _context.Servers.FirstOrDefaultAsync(x => x.Uuid == uuid && x.NodeId == nodeId, cancellationToken)
                     ?? throw HttpApiException.NotFound("Server not found");

        var now = _timeProvider.GetUtcNow();

        server.Status = successful ? ServerStatus.Ready : ServerStatus.InstallFailed;
        server.UpdatedAt = now;

        if (successful)
            server.InstalledAt = now;

        await _context.SaveChangesAsync(cancellationToken);
    }

    // ─── Responses ──────────────────────────────────────────────────────────

    public static AdminServerDetailResponse ToAdminDetail(Server server)
    {
        var response = FillAdminSummary(server, new AdminServerDetailResponse());

        response.Description = server.Description;
        response.OwnerEmail = server.Owner.Email;
        response.DockerImage = server.DockerImage;
        response.StartupCommand = server.StartupCommand;
        response.ShardStartupCommand = server.Shard.StartupCommand;
        response.ShardDockerImages = server.Shard.GetDockerImages();
        response.FeatureLimits = ToFeatureLimits(server);
        response.Allocations = ToAllocations(server);
        response.Variables = ToVariables(server, viewableOnly: false);
        response.InstalledAt = server.InstalledAt;
        response.UpdatedAt = server.UpdatedAt;

        return response;
    }

    public static ServerSummaryResponse ToSummary(Server server, int userId)
        => FillSummary(server, userId, new ServerSummaryResponse());

    /// <param name="permissions">Effective permissions of the user, variables are only included with startup.read</param>
    public static ServerDetailResponse ToDetail(Server server, int userId, IReadOnlyList<string> permissions)
    {
        var response = FillSummary(server, userId, new ServerDetailResponse());

        response.OwnerUsername = server.Owner.Username;
        response.NodeFqdn = server.Node.Fqdn;
        response.SftpPort = server.Node.SftpPort;
        response.DockerImage = server.DockerImage;
        response.StartupPreview = ServerConfigurationBuilder.BuildStartupPreview(server);
        response.FeatureLimits = ToFeatureLimits(server);
        response.Allocations = ToAllocations(server);
        response.Variables = ServerPermissions.Has(permissions, ServerPermissions.StartupRead) ? ToVariables(server, viewableOnly: true) : [];
        response.Permissions = permissions.ToList();

        return response;
    }

    private static T FillAdminSummary<T>(Server server, T response) where T : AdminServerSummaryResponse
    {
        response.Id = server.Id;
        response.Uuid = server.Uuid;
        response.Name = server.Name;
        response.OwnerId = server.OwnerId;
        response.OwnerUsername = server.Owner.Username;
        response.NodeId = server.NodeId;
        response.NodeName = server.Node.Name;
        response.ShardId = server.ShardId;
        response.ShardName = server.Shard.Name;
        response.Status = server.Status;
        response.Suspended = server.Suspended;
        response.PrimaryAllocation = FormatPrimary(server);
        response.Limits = ServerConfigurationBuilder.ToLimits(server);
        response.CreatedAt = server.CreatedAt;
        response.ExternalId = server.ExternalId;
        return response;
    }

    private static T FillSummary<T>(Server server, int userId, T response) where T : ServerSummaryResponse
    {
        response.Id = server.Id;
        response.Uuid = server.Uuid;
        response.Name = server.Name;
        response.Description = server.Description;
        response.NodeName = server.Node.Name;
        response.ShardName = server.Shard.Name;
        response.Status = server.Status;
        response.Suspended = server.Suspended;
        response.PrimaryAllocation = FormatPrimary(server);
        response.Limits = ServerConfigurationBuilder.ToLimits(server);
        response.IsOwner = server.OwnerId == userId;
        return response;
    }

    private static string? FormatPrimary(Server server)
    {
        var primary = ServerConfigurationBuilder.GetPrimaryAllocation(server);
        return primary == null ? null : ServerConfigurationBuilder.FormatAddress(primary.IpAddress, primary.Port);
    }

    private static ServerFeatureLimitsDto ToFeatureLimits(Server server) => new()
    {
        Databases = server.DatabaseLimit,
        Allocations = server.AllocationLimit,
        Backups = server.BackupLimit
    };

    public static List<ServerAllocationDto> ToAllocations(Server server)
        => ServerConfigurationBuilder.GetAllocations(server).Select(x => new ServerAllocationDto
        {
            Id = x.Id,
            IpAddress = x.IpAddress,
            Port = x.Port,
            Notes = x.Notes,
            IsPrimary = x.Id == server.PrimaryAllocationId
        }).ToList();

    private static List<ServerVariableDto> ToVariables(Server server, bool viewableOnly)
    {
        var values = server.Variables.ToDictionary(x => x.ShardVariableId, x => x.Value);

        return server.Shard.Variables
            .Where(x => !viewableOnly || x.UserViewable)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new ServerVariableDto
            {
                ShardVariableId = x.Id,
                Name = x.Name,
                Description = x.Description,
                EnvVariable = x.EnvVariable,
                DefaultValue = x.DefaultValue,
                Value = values.TryGetValue(x.Id, out var value) ? value : null,
                UserViewable = x.UserViewable,
                UserEditable = x.UserEditable,
                Rules = x.Rules
            }).ToList();
    }

    // ─── Validation and helpers ─────────────────────────────────────────────

    public async Task EnsureExternalIdAvailableAsync(string? externalId, int? exceptServerId, CancellationToken cancellationToken)
    {
        if (externalId != null && await _context.Servers.AnyAsync(x => x.Id != exceptServerId && x.ExternalId == externalId, cancellationToken))
            throw ExternalIds.InUse();
    }

    private async Task<Shard> LoadShardAsync(int shardId, CancellationToken cancellationToken)
        => await _context.Shards.Include(x => x.Variables).FirstOrDefaultAsync(x => x.Id == shardId, cancellationToken)
           ?? throw HttpApiException.BadRequest("Shard not found", "The selected shard does not exist");

    /// <summary>The image must be one of the shard's images, null = the shard's default image</summary>
    public static string ResolveDockerImage(Shard shard, string? requested)
    {
        var images = shard.GetDockerImages().Values.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        if (!string.IsNullOrWhiteSpace(shard.DefaultDockerImage))
            images.Add(shard.DefaultDockerImage);

        if (string.IsNullOrWhiteSpace(requested))
        {
            return images.FirstOrDefault(x => x == shard.DefaultDockerImage)
                   ?? images.FirstOrDefault()
                   ?? throw HttpApiException.BadRequest("No docker image", "The shard has no docker image");
        }

        var image = requested.Trim();

        if (!images.Contains(image, StringComparer.Ordinal))
            throw HttpApiException.BadRequest("Invalid docker image", $"'{image}' is not one of the images of the shard {shard.Name}");

        return image;
    }

    private static string? ValidateStartupCommand(Shard shard, string? startupCommand)
    {
        if (string.IsNullOrWhiteSpace(startupCommand))
            return null;

        var command = startupCommand.Trim();
        var errors = StartupCommandResolver.ValidatePlaceholders(command, shard.Variables.Select(x => x.EnvVariable));

        if (errors.Count > 0)
            throw HttpApiException.BadRequest("Invalid placeholders in startup command", string.Join(" ", errors));

        return command;
    }

    /// <summary>
    /// Value of every shard variable: the requested one, else the current one (same EnvVariable), else the default.
    /// Admins may set every variable, all rules are validated. Keys that are no variable of the shard are ignored.
    /// </summary>
    public static Dictionary<string, string> ResolveVariables(Shard shard, IDictionary<string, string>? requested, IDictionary<string, string>? current)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new Dictionary<string, string>();

        foreach (var variable in shard.Variables.OrderBy(x => x.SortOrder))
        {
            string? value = null;

            if (requested != null && requested.TryGetValue(variable.EnvVariable, out var requestedValue))
                value = requestedValue?.Trim();
            else if (current != null && current.TryGetValue(variable.EnvVariable, out var currentValue))
                value = currentValue;

            value ??= variable.DefaultValue ?? "";

            var (isValid, error) = ShardVariableValidator.ValidateValue(value, variable.Rules, variable.Name);

            if (!isValid)
                errors[variable.EnvVariable] = error ?? "Invalid value";

            values[variable.EnvVariable] = value;
        }

        ThrowIfInvalid(errors);

        return values;
    }

    private static void ThrowIfInvalid(Dictionary<string, string> errors)
    {
        if (errors.Count > 0)
            throw new HttpApiException(StatusCodes.Status400BadRequest, "Invalid variables", string.Join(" ", errors.Values)) { Errors = errors };
    }

    /// <summary>
    /// Loads the primary and additional allocations. All must exist, belong to the node and be free
    /// (or already belong to the server when serverId is set).
    /// </summary>
    public async Task<List<Allocation>> LoadAllocationsAsync(int nodeId, int? serverId, int primaryId, IEnumerable<int>? additionalIds, CancellationToken cancellationToken)
    {
        var ids = new[] { primaryId }.Concat(additionalIds ?? []).Distinct().ToList();

        var allocations = await _context.Allocations.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

        var missing = ids.Except(allocations.Select(x => x.Id)).ToList();

        if (missing.Count > 0)
            throw HttpApiException.BadRequest("Allocation not found", $"Allocation(s) {string.Join(", ", missing)} do not exist");

        var foreign = allocations.Where(x => x.NodeId != nodeId).ToList();

        if (foreign.Count > 0)
            throw HttpApiException.BadRequest("Allocation of another node", $"{Describe(foreign)} does not belong to the server's node");

        var taken = allocations.Where(x => x.ServerId != null && x.ServerId != serverId).ToList();

        if (taken.Count > 0)
            throw HttpApiException.Conflict("Allocation in use", $"{Describe(taken)} is already assigned to another server");

        return allocations;
    }

    /// <summary>Assigns exactly the given allocations to the server, previous ones are freed</summary>
    private static void AssignAllocations(Server server, List<Allocation> allocations, int primaryId)
    {
        foreach (var allocation in server.Allocations.Where(x => allocations.All(a => a.Id != x.Id)).ToList())
        {
            allocation.ServerId = null;
            allocation.Server = null;
            server.Allocations.Remove(allocation);
        }

        foreach (var allocation in allocations)
        {
            allocation.ServerId = server.Id;
            allocation.Server = server;

            if (!server.Allocations.Contains(allocation))
                server.Allocations.Add(allocation);
        }

        server.PrimaryAllocationId = primaryId;
        server.PrimaryAllocation = allocations.First(x => x.Id == primaryId);
    }

    private static void ApplyLimits(Server server, ServerLimitsDto limits, ServerFeatureLimitsDto featureLimits)
    {
        server.Cpu = limits.Cpu;
        server.Memory = limits.Memory;
        server.Swap = limits.Swap;
        server.Disk = limits.Disk;
        server.IoWeight = limits.IoWeight;
        server.Threads = NullIfEmpty(limits.Threads);
        server.OomKillerDisabled = limits.OomKillerDisabled;

        server.DatabaseLimit = featureLimits.Databases;
        server.AllocationLimit = featureLimits.Allocations;
        server.BackupLimit = featureLimits.Backups;
    }

    /// <summary>Stores one value per shard variable and removes values of variables the shard doesn't have</summary>
    private static void ApplyVariables(Server server, Shard shard, Dictionary<string, string> values)
    {
        var ids = shard.Variables.Select(x => x.Id).ToHashSet();
        server.Variables.RemoveAll(x => !ids.Contains(x.ShardVariableId));

        foreach (var variable in shard.Variables)
        {
            if (!values.TryGetValue(variable.EnvVariable, out var value))
                continue;

            var existing = server.Variables.FirstOrDefault(x => x.ShardVariableId == variable.Id);

            if (existing != null)
                existing.Value = value;
            else
                server.Variables.Add(new ServerVariable { ShardVariable = variable, ShardVariableId = variable.Id, Value = value });
        }
    }

    /// <summary>Saves the changes and pushes the new configuration, nothing is saved when the demon fails</summary>
    public async Task SaveAndPushAsync(Server server, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await CallNodeAsync(() => _nodeClient.UpdateServerAsync(server.Node, BuildConfiguration(server), cancellationToken), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task CallNodeAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        try
        {
            await action();
        }
        catch (Exception e) when (NodeClient.IsNodeError(e, cancellationToken))
        {
            throw NodeClient.ToApiException(e);
        }
    }

    /// <summary>409 while the server is suspended or being installed</summary>
    public static void EnsureUsable(Server server)
    {
        if (server.Suspended)
            throw HttpApiException.Conflict("The server is suspended");

        if (server.Status == ServerStatus.Installing)
            throw HttpApiException.Conflict("The server is being installed");
    }

    private static string Describe(IEnumerable<Allocation> allocations)
        => string.Join(", ", allocations.Select(x => ServerConfigurationBuilder.FormatAddress(x.IpAddress, x.Port)));

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
