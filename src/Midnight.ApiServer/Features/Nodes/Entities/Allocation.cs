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

    // TODO: Phase 3 - foreign key to the server the allocation is assigned to
    public int? ServerId { get; set; }
}
