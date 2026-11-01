using System.ComponentModel.DataAnnotations;
using Midnight.Shared.Http.Admin;
using Midnight.Shared.Http.Servers;

namespace Midnight.Shared.Http.Application;

/// <summary>
/// Requests of the application api (/api/application/v1). Responses reuse the admin models
/// (AdminUserResponse, AdminServerDetailResponse, NodeResponse, ...), which contain the ExternalId.
/// Update requests are partial: null = keep the current value.
/// </summary>
public static class ApplicationApi
{
    public const string BasePath = "/api/application/v1";

    /// <summary>Tokens look like "mda_{identifier}_{secret}"</summary>
    public const string TokenPrefix = "mda_";

    public const int ExternalIdMaxLength = 191;
}

// ─── Users ──────────────────────────────────────────────────────────────────

public class ApplicationCreateUserRequest
{
    [Required, MinLength(3), MaxLength(32), RegularExpression("^[a-zA-Z0-9_.-]+$")]
    public string Username { get; set; } = "";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    /// <summary>null = a random password is generated (see PasswordGenerated in the response)</summary>
    [MinLength(8), MaxLength(256)]
    public string? Password { get; set; }

    [MaxLength(ApplicationApi.ExternalIdMaxLength)]
    public string? ExternalId { get; set; }

    /// <summary>Only roles whose permissions the creator of the key has (and admin.roles.write)</summary>
    public List<int> RoleIds { get; set; } = [];

    /// <summary>Sends the "reset password" mail so the user can choose a password, useful with a generated password</summary>
    public bool SendPasswordReset { get; set; }
}

public class ApplicationCreatedUserResponse : AdminUserResponse
{
    /// <summary>True when no password was given and a random one was set</summary>
    public bool PasswordGenerated { get; set; }
}

public class ApplicationUpdateUserRequest
{
    [MinLength(3), MaxLength(32), RegularExpression("^[a-zA-Z0-9_.-]+$")]
    public string? Username { get; set; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    [MinLength(8), MaxLength(256)]
    public string? Password { get; set; }

    /// <summary>Empty string removes the external id</summary>
    [MaxLength(ApplicationApi.ExternalIdMaxLength)]
    public string? ExternalId { get; set; }

    public bool? IsLocked { get; set; }

    /// <summary>Replaces the roles of the user</summary>
    public List<int>? RoleIds { get; set; }
}

// ─── Servers ────────────────────────────────────────────────────────────────

/// <summary>Picks a free allocation automatically instead of PrimaryAllocationId</summary>
public class ApplicationDeployOptions
{
    /// <summary>Only this node (combined with NodeIds)</summary>
    public int? NodeId { get; set; }

    /// <summary>Any of these nodes, empty and no NodeId = every node</summary>
    public List<int> NodeIds { get; set; } = [];

    /// <summary>Ports or ranges like "25565" or "27015-27030", empty = any port</summary>
    [MaxLength(32)]
    public List<string> Ports { get; set; } = [];

    /// <summary>Further free allocations of the same node and ip</summary>
    [Range(0, 100)]
    public int AdditionalAllocations { get; set; }
}

public class ApplicationCreateServerRequest
{
    [Required, MinLength(1), MaxLength(64)]
    public string Name { get; set; } = "";

    [MaxLength(512)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue)]
    public int OwnerId { get; set; }

    /// <summary>Optional with PrimaryAllocationId (taken from the allocation), ignored with Deploy</summary>
    [Range(1, int.MaxValue)]
    public int? NodeId { get; set; }

    [Range(1, int.MaxValue)]
    public int ShardId { get; set; }

    [MaxLength(255)]
    public string? DockerImage { get; set; }

    [MaxLength(4096)]
    public string? StartupCommand { get; set; }

    [Required]
    public ServerLimitsDto Limits { get; set; } = new();

    [Required]
    public ServerFeatureLimitsDto FeatureLimits { get; set; } = new();

    /// <summary>Either this or Deploy</summary>
    [Range(1, int.MaxValue)]
    public int? PrimaryAllocationId { get; set; }

    public List<int> AdditionalAllocationIds { get; set; } = [];

    public ApplicationDeployOptions? Deploy { get; set; }

    public Dictionary<string, string> Variables { get; set; } = [];

    public bool StartOnCompletion { get; set; }

    public bool SkipInstall { get; set; }

    [MaxLength(ApplicationApi.ExternalIdMaxLength)]
    public string? ExternalId { get; set; }
}

public class ApplicationUpdateServerDetailsRequest
{
    [MinLength(1), MaxLength(64)]
    public string? Name { get; set; }

    /// <summary>Empty string removes the description</summary>
    [MaxLength(512)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue)]
    public int? OwnerId { get; set; }

    /// <summary>Empty string removes the external id</summary>
    [MaxLength(ApplicationApi.ExternalIdMaxLength)]
    public string? ExternalId { get; set; }
}

/// <summary>Partial <see cref="ServerLimitsDto"/></summary>
public class ApplicationServerLimitsUpdate
{
    [Range(0, 100_000)]
    public int? Cpu { get; set; }

    [Range(64, 10_485_760)]
    public int? Memory { get; set; }

    [Range(-1, 10_485_760)]
    public int? Swap { get; set; }

    [Range(0, 104_857_600)]
    public int? Disk { get; set; }

    [Range(10, 1000)]
    public int? IoWeight { get; set; }

    /// <summary>Empty string = all cores</summary>
    [MaxLength(64), RegularExpression(@"^$|^[0-9]+(-[0-9]+)?(,[0-9]+(-[0-9]+)?)*$")]
    public string? Threads { get; set; }

    public bool? OomKillerDisabled { get; set; }
}

/// <summary>Partial <see cref="ServerFeatureLimitsDto"/></summary>
public class ApplicationFeatureLimitsUpdate
{
    [Range(0, 1000)]
    public int? Databases { get; set; }

    [Range(0, 1000)]
    public int? Allocations { get; set; }

    [Range(0, 1000)]
    public int? Backups { get; set; }
}

public class ApplicationUpdateServerBuildRequest
{
    public ApplicationServerLimitsUpdate? Limits { get; set; }

    public ApplicationFeatureLimitsUpdate? FeatureLimits { get; set; }

    [Range(1, int.MaxValue)]
    public int? PrimaryAllocationId { get; set; }

    /// <summary>Replaces all allocations besides the primary one</summary>
    public List<int>? AdditionalAllocationIds { get; set; }
}

public class ApplicationUpdateServerStartupRequest
{
    /// <summary>Changing the shard keeps matching variables and adds the new ones with defaults</summary>
    [Range(1, int.MaxValue)]
    public int? ShardId { get; set; }

    /// <summary>null = keep (or the new shard's default image when the shard changes)</summary>
    [MaxLength(255)]
    public string? DockerImage { get; set; }

    /// <summary>Empty string = the shard's startup command</summary>
    [MaxLength(4096)]
    public string? StartupCommand { get; set; }

    /// <summary>EnvVariable => value, other variables keep their value</summary>
    public Dictionary<string, string>? Variables { get; set; }
}

// ─── Nodes ──────────────────────────────────────────────────────────────────

public class ApplicationUpdateNodeRequest
{
    [MinLength(1), MaxLength(64)]
    public string? Name { get; set; }

    [MaxLength(255), RegularExpression(@"^[a-zA-Z0-9]([a-zA-Z0-9.\-]*[a-zA-Z0-9])?$|^\[?[0-9a-fA-F:]+\]?$")]
    public string? Fqdn { get; set; }

    [Range(1, 65535)]
    public int? HttpPort { get; set; }

    [Range(1, 65535)]
    public int? SftpPort { get; set; }

    public bool? UseSsl { get; set; }
}
