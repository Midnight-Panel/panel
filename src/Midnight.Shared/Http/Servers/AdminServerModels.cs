using System.ComponentModel.DataAnnotations;
using Midnight.Shared.Enums;

namespace Midnight.Shared.Http.Servers;

// ─── Shared parts ───────────────────────────────────────────────────────────

/// <summary>Resource limits of a server container</summary>
public class ServerLimitsDto
{
    /// <summary>CPU limit in percent of one core (100 = 1 core, 250 = 2.5 cores), 0 = unlimited</summary>
    [Range(0, 100_000)]
    public int Cpu { get; set; } = 100;

    /// <summary>Memory in MiB</summary>
    [Range(64, 10_485_760)]
    public int Memory { get; set; } = 1024;

    /// <summary>Swap in MiB, 0 = disabled, -1 = unlimited</summary>
    [Range(-1, 10_485_760)]
    public int Swap { get; set; }

    /// <summary>Disk space in MiB, 0 = unlimited</summary>
    [Range(0, 104_857_600)]
    public int Disk { get; set; } = 5120;

    /// <summary>Block io weight, 10-1000 (relative to other servers on the node)</summary>
    [Range(10, 1000)]
    public int IoWeight { get; set; } = 500;

    /// <summary>Pin to cpu cores, e.g. "0", "0-3" or "0,2,4". Empty = all cores</summary>
    [MaxLength(64), RegularExpression(@"^$|^[0-9]+(-[0-9]+)?(,[0-9]+(-[0-9]+)?)*$")]
    public string? Threads { get; set; }

    /// <summary>Don't kill the container when it runs out of memory (it freezes instead)</summary>
    public bool OomKillerDisabled { get; set; }
}

/// <summary>How many databases, extra allocations and backups the owner may create</summary>
public class ServerFeatureLimitsDto
{
    [Range(0, 1000)]
    public int Databases { get; set; }

    [Range(0, 1000)]
    public int Allocations { get; set; }

    [Range(0, 1000)]
    public int Backups { get; set; }
}

public class ServerAllocationDto
{
    public int Id { get; set; }
    public string IpAddress { get; set; } = "";
    public int Port { get; set; }
    public string? Notes { get; set; }
    public bool IsPrimary { get; set; }
}

public class ServerVariableDto
{
    public int ShardVariableId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string EnvVariable { get; set; } = "";
    public string? DefaultValue { get; set; }

    /// <summary>The value of this server, null = the default</summary>
    public string? Value { get; set; }

    public bool UserViewable { get; set; }
    public bool UserEditable { get; set; }
    public string Rules { get; set; } = "";
}

// ─── Admin: /api/admin/servers ──────────────────────────────────────────────

public class AdminServerSummaryResponse
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public string Name { get; set; } = "";
    public int OwnerId { get; set; }
    public string OwnerUsername { get; set; } = "";
    public int NodeId { get; set; }
    public string NodeName { get; set; } = "";
    public int ShardId { get; set; }
    public string ShardName { get; set; } = "";
    public ServerStatus Status { get; set; }
    public bool Suspended { get; set; }

    /// <summary>"ip:port" of the primary allocation</summary>
    public string? PrimaryAllocation { get; set; }

    public ServerLimitsDto Limits { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Id of the server in an external system (e.g. a billing system), set via the application api</summary>
    public string? ExternalId { get; set; }
}

public class AdminServerDetailResponse : AdminServerSummaryResponse
{
    public string? Description { get; set; }
    public string OwnerEmail { get; set; } = "";
    public string DockerImage { get; set; } = "";

    /// <summary>Own startup command, null = the shard's command</summary>
    public string? StartupCommand { get; set; }

    /// <summary>The shard's startup command, shown as placeholder</summary>
    public string ShardStartupCommand { get; set; } = "";

    /// <summary>Label => image of the shard, for the image select</summary>
    public Dictionary<string, string> ShardDockerImages { get; set; } = [];

    public ServerFeatureLimitsDto FeatureLimits { get; set; } = new();
    public List<ServerAllocationDto> Allocations { get; set; } = [];
    public List<ServerVariableDto> Variables { get; set; } = [];
    public DateTimeOffset? InstalledAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CreateServerRequest
{
    [Required, MinLength(1), MaxLength(64)]
    public string Name { get; set; } = "";

    [MaxLength(512)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue)]
    public int OwnerId { get; set; }

    [Range(1, int.MaxValue)]
    public int NodeId { get; set; }

    [Range(1, int.MaxValue)]
    public int ShardId { get; set; }

    /// <summary>Must be one of the shard's images, null = the shard's default image</summary>
    [MaxLength(255)]
    public string? DockerImage { get; set; }

    /// <summary>null = the shard's startup command</summary>
    [MaxLength(4096)]
    public string? StartupCommand { get; set; }

    [Required]
    public ServerLimitsDto Limits { get; set; } = new();

    [Required]
    public ServerFeatureLimitsDto FeatureLimits { get; set; } = new();

    /// <summary>Free allocation of the node, becomes the main ip:port</summary>
    [Range(1, int.MaxValue)]
    public int PrimaryAllocationId { get; set; }

    public List<int> AdditionalAllocationIds { get; set; } = [];

    /// <summary>EnvVariable => value. Missing variables get their default value.</summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>Start the server once the installation finished</summary>
    public bool StartOnCompletion { get; set; }

    /// <summary>Don't run the shard's install script</summary>
    public bool SkipInstall { get; set; }
}

/// <summary>PUT /api/admin/servers/{id}/details</summary>
public class UpdateServerDetailsRequest
{
    [Required, MinLength(1), MaxLength(64)]
    public string Name { get; set; } = "";

    [MaxLength(512)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue)]
    public int OwnerId { get; set; }
}

/// <summary>PUT /api/admin/servers/{id}/build</summary>
public class UpdateServerBuildRequest
{
    [Required]
    public ServerLimitsDto Limits { get; set; } = new();

    [Required]
    public ServerFeatureLimitsDto FeatureLimits { get; set; } = new();

    [Range(1, int.MaxValue)]
    public int PrimaryAllocationId { get; set; }

    /// <summary>All allocations besides the primary one (replaces the current list)</summary>
    public List<int> AdditionalAllocationIds { get; set; } = [];
}

/// <summary>PUT /api/admin/servers/{id}/startup</summary>
public class UpdateServerStartupRequest
{
    /// <summary>Changing the shard keeps matching variables and adds the new ones with defaults</summary>
    [Range(1, int.MaxValue)]
    public int ShardId { get; set; }

    [Required, MaxLength(255)]
    public string DockerImage { get; set; } = "";

    /// <summary>null = the shard's startup command</summary>
    [MaxLength(4096)]
    public string? StartupCommand { get; set; }

    /// <summary>EnvVariable => value</summary>
    public Dictionary<string, string> Variables { get; set; } = [];
}
