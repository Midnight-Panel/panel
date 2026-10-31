using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Nodes;

namespace Midnight.ApiServer.Features.Nodes.Services;

public class NodeService
{
    private readonly DataContext _context;
    private readonly IDataProtector _protector;
    private readonly IOptionsMonitor<AppConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;

    public NodeService(DataContext context, IDataProtectionProvider dataProtection, IOptionsMonitor<AppConfiguration> configuration, TimeProvider timeProvider)
    {
        _context = context;
        _protector = dataProtection.CreateProtector("Midnight.NodeToken.v1");
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<PagedResult<NodeResponse>> ListAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        var nodes = _context.Nodes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            nodes = nodes.Where(x => x.Name.ToLower().Contains(search) || x.Fqdn.ToLower().Contains(search));
        }

        nodes = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", false) => nodes.OrderBy(x => x.Name),
            ("name", true) => nodes.OrderByDescending(x => x.Name),
            ("fqdn", false) => nodes.OrderBy(x => x.Fqdn),
            ("fqdn", true) => nodes.OrderByDescending(x => x.Fqdn),
            (_, true) => nodes.OrderByDescending(x => x.Id),
            _ => nodes.OrderBy(x => x.Id)
        };

        var total = await nodes.CountAsync(cancellationToken);

        var items = await nodes
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(x => new NodeResponse
            {
                Id = x.Id,
                Name = x.Name,
                Fqdn = x.Fqdn,
                HttpPort = x.HttpPort,
                SftpPort = x.SftpPort,
                UseSsl = x.UseSsl,
                AllocationCount = x.Allocations.Count,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<NodeResponse> { Items = items, Page = query.SafePage, PageSize = query.SafePageSize, TotalCount = total };
    }

    public async Task<Node> GetAsync(int id, CancellationToken cancellationToken)
        => await _context.Nodes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
           ?? throw HttpApiException.NotFound("Node not found");

    public async Task<NodeResponse> GetResponseAsync(int id, CancellationToken cancellationToken)
    {
        var node = await GetAsync(id, cancellationToken);
        var allocations = await _context.Allocations.CountAsync(x => x.NodeId == id, cancellationToken);

        return ToResponse(node, allocations);
    }

    public async Task<NodeResponse> CreateAsync(NodeRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureUniqueAsync(name, null, cancellationToken);

        var node = new Node
        {
            Name = name,
            Fqdn = request.Fqdn.Trim().ToLowerInvariant(),
            HttpPort = request.HttpPort,
            SftpPort = request.SftpPort,
            UseSsl = request.UseSsl,
            CreatedAt = _timeProvider.GetUtcNow()
        };

        AssignNewToken(node);

        _context.Nodes.Add(node);
        await _context.SaveChangesAsync(cancellationToken);

        return ToResponse(node, 0);
    }

    public async Task<NodeResponse> UpdateAsync(int id, NodeRequest request, CancellationToken cancellationToken)
    {
        var node = await GetAsync(id, cancellationToken);

        var name = request.Name.Trim();
        await EnsureUniqueAsync(name, id, cancellationToken);

        node.Name = name;
        node.Fqdn = request.Fqdn.Trim().ToLowerInvariant();
        node.HttpPort = request.HttpPort;
        node.SftpPort = request.SftpPort;
        node.UseSsl = request.UseSsl;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var node = await GetAsync(id, cancellationToken);

        if (await _context.Allocations.AnyAsync(x => x.NodeId == id && x.ServerId != null, cancellationToken))
            throw HttpApiException.Conflict("The node still has servers", "Delete or move all servers of this node first");

        _context.Nodes.Remove(node);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Generates a new token. The daemon has to be reconfigured afterwards.</summary>
    public async Task ResetTokenAsync(int id, CancellationToken cancellationToken)
    {
        var node = await GetAsync(id, cancellationToken);

        AssignNewToken(node);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public string GetToken(Node node) => _protector.Unprotect(node.Token);

    /// <summary>Finds the node of a token sent by a daemon. Null if the token is unknown or wrong.</summary>
    public async Task<Node?> AuthenticateAsync(string tokenId, string token, CancellationToken cancellationToken)
    {
        var node = await _context.Nodes.AsNoTracking().FirstOrDefaultAsync(x => x.TokenId == tokenId, cancellationToken);

        if (node == null)
            return null;

        var expected = System.Text.Encoding.UTF8.GetBytes(GetToken(node));
        var actual = System.Text.Encoding.UTF8.GetBytes(token);

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual) ? node : null;
    }

    public NodeSetupResponse GetSetup(Node node)
    {
        var configuration = new
        {
            panelUrl = _configuration.CurrentValue.PublicUrl.TrimEnd('/'),
            tokenId = node.TokenId,
            token = GetToken(node),
            httpPort = node.HttpPort,
            sftpPort = node.SftpPort,
            dataPath = "/var/lib/midnight",
            ssl = new
            {
                enabled = node.UseSsl,
                certificatePath = node.UseSsl ? $"/etc/letsencrypt/live/{node.Fqdn}/fullchain.pem" : "",
                keyPath = node.UseSsl ? $"/etc/letsencrypt/live/{node.Fqdn}/privkey.pem" : ""
            }
        };

        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });

        // TODO: Phase 8 - one line installer (install.sh) that also installs docker and the systemd unit
        var command =
            "sudo mkdir -p /etc/midnight && sudo tee /etc/midnight/config.json > /dev/null <<'EOF'\n" +
            json +
            "\nEOF\nsudo chmod 600 /etc/midnight/config.json";

        return new NodeSetupResponse { Configuration = json, Command = command };
    }

    private void AssignNewToken(Node node)
    {
        node.TokenId = SecureTokens.GenerateAlphanumeric(16);
        node.Token = _protector.Protect(SecureTokens.GenerateAlphanumeric(64));
    }

    private async Task EnsureUniqueAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        var lower = name.ToLower();

        if (await _context.Nodes.AnyAsync(x => x.Id != exceptId && x.Name.ToLower() == lower, cancellationToken))
            throw HttpApiException.Conflict("A node with this name already exists");
    }

    private static NodeResponse ToResponse(Node node, int allocations) => new()
    {
        Id = node.Id,
        Name = node.Name,
        Fqdn = node.Fqdn,
        HttpPort = node.HttpPort,
        SftpPort = node.SftpPort,
        UseSsl = node.UseSsl,
        AllocationCount = allocations,
        CreatedAt = node.CreatedAt
    };
}
