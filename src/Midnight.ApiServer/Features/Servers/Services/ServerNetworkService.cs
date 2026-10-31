using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>
/// Allocations the user manages: FeatureLimits.Allocations additional ports besides the primary one, assigned randomly from
/// the free allocations of the node (same ip as the primary preferred). Changes push the configuration (applied on next start).
/// </summary>
public class ServerNetworkService
{
    private const int MaxAssignAttempts = 5;

    private readonly DataContext _context;
    private readonly ServerService _serverService;
    private readonly NodeClient _nodeClient;

    public ServerNetworkService(DataContext context, ServerService serverService, NodeClient nodeClient)
    {
        _context = context;
        _serverService = serverService;
        _nodeClient = nodeClient;
    }

    public static ServerAllocationListResponse ToList(Server server) => new()
    {
        Allocations = ServerService.ToAllocations(server),
        Limit = server.AllocationLimit
    };

    public static ServerAllocationDto ToDto(Server server, Allocation allocation) => new()
    {
        Id = allocation.Id,
        IpAddress = allocation.IpAddress,
        Port = allocation.Port,
        Notes = allocation.Notes,
        IsPrimary = allocation.Id == server.PrimaryAllocationId
    };

    /// <summary>409 at the limit (0 = no additional allocations) or when the node has no free allocation</summary>
    public async Task<Allocation> AssignAsync(Server server, CancellationToken cancellationToken)
    {
        EnsureNotSuspended(server);

        var additional = server.Allocations.Count(x => x.Id != server.PrimaryAllocationId);

        if (additional >= server.AllocationLimit)
            throw HttpApiException.Conflict("Allocation limit reached", $"This server can have at most {server.AllocationLimit} additional allocation(s)");

        var primaryIp = ServerConfigurationBuilder.GetPrimaryAllocation(server)?.IpAddress;

        var free = await _context.Allocations
            .AsNoTracking()
            .Where(x => x.NodeId == server.NodeId && x.ServerId == null)
            .Select(x => new { x.Id, x.IpAddress })
            .ToListAsync(cancellationToken);

        // Same ip as the primary allocation first, random order within the groups
        var candidates = free
            .OrderBy(x => x.IpAddress == primaryIp ? 0 : 1)
            .ThenBy(_ => Random.Shared.Next())
            .Select(x => x.Id)
            .Take(MaxAssignAttempts)
            .ToList();

        foreach (var id in candidates)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // Conditional update: a parallel request can't assign the same allocation to another server
            var updated = await _context.Allocations
                .Where(x => x.Id == id && x.ServerId == null)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.ServerId, server.Id), cancellationToken);

            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                continue;
            }

            // Loading it attaches it to server.Allocations
            var allocation = await _context.Allocations.FirstAsync(x => x.Id == id, cancellationToken);

            if (!server.Allocations.Contains(allocation))
                server.Allocations.Add(allocation);

            await ServerService.CallNodeAsync(() => _nodeClient.UpdateServerAsync(server.Node, _serverService.BuildConfiguration(server), cancellationToken), cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return allocation;
        }

        throw HttpApiException.Conflict("No free allocation available", "The node has no free allocation, ask an administrator to add more");
    }

    /// <summary>Notes are not part of the demon configuration, nothing is pushed</summary>
    public async Task<Allocation> UpdateNotesAsync(Server server, int id, string? notes, CancellationToken cancellationToken)
    {
        var allocation = Get(server, id);

        allocation.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        return allocation;
    }

    public async Task<Allocation> SetPrimaryAsync(Server server, int id, CancellationToken cancellationToken)
    {
        EnsureNotSuspended(server);

        var allocation = Get(server, id);

        if (server.PrimaryAllocationId == allocation.Id)
            return allocation;

        server.PrimaryAllocationId = allocation.Id;
        server.PrimaryAllocation = allocation;

        await _serverService.SaveAndPushAsync(server, cancellationToken);

        return allocation;
    }

    /// <summary>The primary allocation can't be removed (409). The allocation is free again afterwards (notes are cleared).</summary>
    public async Task<Allocation> RemoveAsync(Server server, int id, CancellationToken cancellationToken)
    {
        EnsureNotSuspended(server);

        var allocation = Get(server, id);

        if (allocation.Id == server.PrimaryAllocationId)
            throw HttpApiException.Conflict("The primary allocation can't be removed", "Make another allocation the primary one first");

        allocation.ServerId = null;
        allocation.Server = null;
        allocation.Notes = null;
        server.Allocations.Remove(allocation);

        await _serverService.SaveAndPushAsync(server, cancellationToken);

        return allocation;
    }

    private static Allocation Get(Server server, int id)
        => server.Allocations.FirstOrDefault(x => x.Id == id) ?? throw HttpApiException.NotFound("Allocation not found");

    private static void EnsureNotSuspended(Server server)
    {
        if (server.Suspended)
            throw HttpApiException.Conflict("The server is suspended");
    }
}
