using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.Shared.Enums;

namespace Midnight.ApiServer.Features.Servers.Entities;

/// <summary>A game or application server running in a container on a node</summary>
public class Server
{
    public int Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>Id in an external system (billing), unique when set. Managed via the application api.</summary>
    public string? ExternalId { get; set; }

    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public int NodeId { get; set; }
    public Node Node { get; set; } = null!;

    public int ShardId { get; set; }
    public Shard Shard { get; set; } = null!;

    /// <summary>The main ip:port (SERVER_IP / SERVER_PORT). Always one of <see cref="Allocations"/>.</summary>
    public int? PrimaryAllocationId { get; set; }
    public Allocation? PrimaryAllocation { get; set; }

    public string DockerImage { get; set; } = "";

    /// <summary>Own startup command, null = the shard's command</summary>
    public string? StartupCommand { get; set; }

    // Resource limits, see ServerLimitsDto

    public int Cpu { get; set; }
    public int Memory { get; set; }
    public int Swap { get; set; }
    public int Disk { get; set; }
    public int IoWeight { get; set; } = 500;
    public string? Threads { get; set; }
    public bool OomKillerDisabled { get; set; }

    // Feature limits, see ServerFeatureLimitsDto

    public int DatabaseLimit { get; set; }
    public int AllocationLimit { get; set; }
    public int BackupLimit { get; set; }

    public ServerStatus Status { get; set; }
    public bool Suspended { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? InstalledAt { get; set; }

    public List<Allocation> Allocations { get; set; } = [];
    public List<ServerVariable> Variables { get; set; } = [];
    public List<Subuser> Subusers { get; set; } = [];
}
