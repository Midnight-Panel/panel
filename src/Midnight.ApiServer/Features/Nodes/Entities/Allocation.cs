using Midnight.ApiServer.Features.Servers.Entities;

namespace Midnight.ApiServer.Features.Nodes.Entities;

/// <summary>An ip and port on a node that can be assigned to a server</summary>
public class Allocation
{
    public int Id { get; set; }

    public int NodeId { get; set; }
    public Node Node { get; set; } = null!;

    public string IpAddress { get; set; } = "";
    public int Port { get; set; }

    /// <summary>Optional label, e.g. "Minecraft query"</summary>
    public string? Notes { get; set; }

    /// <summary>The server the allocation is assigned to, null = free</summary>
    public int? ServerId { get; set; }
    public Server? Server { get; set; }
}
