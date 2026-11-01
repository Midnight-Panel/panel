using System.Net;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Nodes;

namespace Midnight.ApiServer.Features.Nodes.Services;

public class AllocationService
{
    /// <summary>Upper bound for one request so a typo like "1-65535" can't flood the database</summary>
    public const int MaxPortsPerRequest = 1000;

    private readonly DataContext _context;

    public AllocationService(DataContext context)
    {
        _context = context;
    }

    /// <param name="free">Only allocations that are not assigned to a server</param>
    public async Task<PagedResult<AllocationResponse>> ListAsync(int nodeId, PagedQuery query, bool free, CancellationToken cancellationToken)
    {
        var allocations = _context.Allocations.AsNoTracking().Where(x => x.NodeId == nodeId);

        if (free)
            allocations = allocations.Where(x => x.ServerId == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            allocations = int.TryParse(search, out var port)
                ? allocations.Where(x => x.Port == port)
                : allocations.Where(x => x.IpAddress.Contains(search) || (x.Notes != null && x.Notes.Contains(search)));
        }

        allocations = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("ipaddress", false) => allocations.OrderBy(x => x.IpAddress).ThenBy(x => x.Port),
            ("ipaddress", true) => allocations.OrderByDescending(x => x.IpAddress).ThenBy(x => x.Port),
            ("port", true) => allocations.OrderByDescending(x => x.Port),
            _ => allocations.OrderBy(x => x.Port).ThenBy(x => x.IpAddress)
        };

        var total = await allocations.CountAsync(cancellationToken);

        var items = await allocations
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(x => new AllocationResponse { Id = x.Id, IpAddress = x.IpAddress, Port = x.Port, Notes = x.Notes, ServerId = x.ServerId })
            .ToListAsync(cancellationToken);

        return new PagedResult<AllocationResponse> { Items = items, Page = query.SafePage, PageSize = query.SafePageSize, TotalCount = total };
    }

    public async Task<CreateAllocationsResponse> CreateAsync(int nodeId, CreateAllocationsRequest request, CancellationToken cancellationToken)
    {
        if (!await _context.Nodes.AnyAsync(x => x.Id == nodeId, cancellationToken))
            throw HttpApiException.NotFound("Node not found");

        if (!IPAddress.TryParse(request.IpAddress.Trim(), out var ip))
            throw HttpApiException.BadRequest("Invalid ip address", $"'{request.IpAddress}' is not a valid ip address");

        var ipAddress = ip.ToString();
        var ports = ParsePorts(request.Ports);

        var existing = await _context.Allocations
            .Where(x => x.NodeId == nodeId && x.IpAddress == ipAddress && ports.Contains(x.Port))
            .Select(x => x.Port)
            .ToListAsync(cancellationToken);

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        foreach (var port in ports.Except(existing))
            _context.Allocations.Add(new Allocation { NodeId = nodeId, IpAddress = ipAddress, Port = port, Notes = notes });

        await _context.SaveChangesAsync(cancellationToken);

        return new CreateAllocationsResponse { Created = ports.Count - existing.Count, Skipped = existing.Count };
    }

    public async Task<int> DeleteAsync(int nodeId, IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
    {
        var allocations = await _context.Allocations
            .Where(x => x.NodeId == nodeId && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (allocations.Any(x => x.ServerId != null))
            throw HttpApiException.Conflict("Allocation in use", "Allocations assigned to a server can't be deleted");

        _context.Allocations.RemoveRange(allocations);
        await _context.SaveChangesAsync(cancellationToken);

        return allocations.Count;
    }

    /// <summary>Parses "25565, 25570-25600" into a distinct, sorted port list</summary>
    public static List<int> ParsePorts(string input)
    {
        var ports = new SortedSet<int>();

        foreach (var range in ParsePortRanges(input))
        {
            if (ports.Count + (range.End - range.Start + 1) > MaxPortsPerRequest)
                throw HttpApiException.BadRequest("Too many ports", $"At most {MaxPortsPerRequest} ports can be added at once");

            for (var port = range.Start; port <= range.End; port++)
                ports.Add(port);
        }

        if (ports.Count == 0)
            throw HttpApiException.BadRequest("Invalid ports", "Enter at least one port");

        return ports.ToList();
    }

    /// <summary>Parses "25565, 25570-25600" into ranges without expanding them (no size limit), empty input = no ranges</summary>
    public static List<PortRange> ParsePortRanges(string input)
    {
        var ranges = new List<PortRange>();

        foreach (var rawPart in input.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.Trim();
            var range = part.Split('-');

            if (range.Length is < 1 or > 2 || !range.All(x => int.TryParse(x, out _)))
                throw HttpApiException.BadRequest("Invalid ports", $"'{part}' is not a port or range like 25565-25600");

            var start = int.Parse(range[0]);
            var end = range.Length == 2 ? int.Parse(range[1]) : start;

            if (start is < 1 or > 65535 || end is < 1 or > 65535)
                throw HttpApiException.BadRequest("Invalid ports", "Ports must be between 1 and 65535");

            if (end < start)
                throw HttpApiException.BadRequest("Invalid ports", $"'{part}' ends before it starts");

            ranges.Add(new PortRange(start, end));
        }

        return ranges;
    }

    /// <summary>
    /// Picks free allocations for a new server: 1 + <paramref name="additionalCount"/> free allocations of the same node and ip
    /// whose ports are in one of the ranges (no ranges = any port). Of the matching nodes the one with the fewest servers wins,
    /// then the lowest ports. No nodes given = every node. 409 when nothing matches.
    /// </summary>
    public async Task<DeployAllocations> FindDeployAllocationsAsync(IReadOnlyCollection<int> nodeIds, IReadOnlyCollection<PortRange> ports, int additionalCount,
        CancellationToken cancellationToken)
    {
        if (additionalCount < 0)
            throw HttpApiException.BadRequest("Invalid number of additional allocations");

        var nodes = nodeIds.Distinct().ToList();

        if (nodes.Count > 0)
        {
            var existing = await _context.Nodes.Where(x => nodes.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken);
            var missing = nodes.Except(existing).ToList();

            if (missing.Count > 0)
                throw HttpApiException.BadRequest("Node not found", $"Node(s) {string.Join(", ", missing)} do not exist");
        }

        var free = _context.Allocations.AsNoTracking().Where(x => x.ServerId == null);

        if (nodes.Count > 0)
            free = free.Where(x => nodes.Contains(x.NodeId));

        if (ports.Count > 0)
        {
            // Narrowed in the database, the exact ranges are checked below
            var min = ports.Min(x => x.Start);
            var max = ports.Max(x => x.End);
            free = free.Where(x => x.Port >= min && x.Port <= max);
        }

        var candidates = (await free.Select(x => new { x.Id, x.NodeId, x.IpAddress, x.Port }).ToListAsync(cancellationToken))
            .Where(x => ports.Count == 0 || ports.Any(r => x.Port >= r.Start && x.Port <= r.End))
            .ToList();

        var needed = 1 + additionalCount;

        var groups = candidates
            .GroupBy(x => (x.NodeId, x.IpAddress))
            .Where(x => x.Count() >= needed)
            .ToList();

        if (groups.Count == 0)
            throw HttpApiException.Conflict("No free allocation", "No node has enough free allocations matching the deploy options");

        var groupNodeIds = groups.Select(x => x.Key.NodeId).Distinct().ToList();

        var serverCounts = await _context.Servers
            .Where(x => groupNodeIds.Contains(x.NodeId))
            .GroupBy(x => x.NodeId)
            .Select(x => new { NodeId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.NodeId, x => x.Count, cancellationToken);

        var chosen = groups
            .OrderBy(x => serverCounts.GetValueOrDefault(x.Key.NodeId))
            .ThenBy(x => x.Key.NodeId)
            .ThenBy(x => x.Min(a => a.Port))
            .ThenBy(x => x.Key.IpAddress, StringComparer.Ordinal)
            .First()
            .OrderBy(x => x.Port)
            .Take(needed)
            .ToList();

        return new DeployAllocations(chosen[0].NodeId, chosen[0].Id, chosen.Skip(1).Select(x => x.Id).ToList());
    }
}

public readonly record struct PortRange(int Start, int End);

/// <summary>Allocations picked by <see cref="AllocationService.FindDeployAllocationsAsync"/></summary>
public record DeployAllocations(int NodeId, int PrimaryAllocationId, List<int> AdditionalAllocationIds);
