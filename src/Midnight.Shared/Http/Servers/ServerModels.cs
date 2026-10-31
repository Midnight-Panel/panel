using System.ComponentModel.DataAnnotations;
using Midnight.Shared.Enums;

namespace Midnight.Shared.Http.Servers;

// ─── User: /api/servers ─────────────────────────────────────────────────────

/// <summary>A server of the current user (or any server for admins with admin.servers.read)</summary>
public class ServerSummaryResponse
{
    /// <summary>Panel id of the server, database names are prefixed with s{Id}_</summary>
    public int Id { get; set; }

    public Guid Uuid { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string NodeName { get; set; } = "";
    public string ShardName { get; set; } = "";
    public ServerStatus Status { get; set; }
    public bool Suspended { get; set; }

    /// <summary>"ip:port" of the primary allocation</summary>
    public string? PrimaryAllocation { get; set; }

    public ServerLimitsDto Limits { get; set; } = new();
    public bool IsOwner { get; set; }
}

public class ServerDetailResponse : ServerSummaryResponse
{
    public string OwnerUsername { get; set; } = "";
    public string NodeFqdn { get; set; } = "";
    public int SftpPort { get; set; }
    public string DockerImage { get; set; } = "";

    /// <summary>The startup command with the variable values filled in (hidden variables stay as placeholders)</summary>
    public string StartupPreview { get; set; } = "";

    public ServerFeatureLimitsDto FeatureLimits { get; set; } = new();
    public List<ServerAllocationDto> Allocations { get; set; } = [];

    /// <summary>Only the variables the user may see</summary>
    public List<ServerVariableDto> Variables { get; set; } = [];

    /// <summary>Server permissions of the current user, ["*"] for the owner and admins (see ServerPermissions)</summary>
    public List<string> Permissions { get; set; } = [];
}

/// <summary>Live values, from the demon. GET /api/servers/{uuid}/resources</summary>
public class ServerResourcesResponse
{
    public ServerState State { get; set; }

    /// <summary>False when the node could not be reached, the values are 0 then</summary>
    public bool NodeOnline { get; set; } = true;

    public ServerStatsDto Stats { get; set; } = new();
}

public class ServerStatsDto
{
    /// <summary>Percent of one core (200 = two full cores)</summary>
    public double CpuPercent { get; set; }

    public long MemoryBytes { get; set; }
    public long MemoryLimitBytes { get; set; }
    public long DiskBytes { get; set; }
    public long NetworkRxBytes { get; set; }
    public long NetworkTxBytes { get; set; }

    /// <summary>Seconds since the container started, 0 when offline</summary>
    public long UptimeSeconds { get; set; }
}

public class PowerRequest
{
    [Required]
    public PowerAction Action { get; set; }
}

public class CommandRequest
{
    [Required, MinLength(1), MaxLength(2048)]
    public string Command { get; set; } = "";
}

/// <summary>PUT /api/servers/{uuid}/variables – only variables the user may edit</summary>
public class UpdateServerVariablesRequest
{
    /// <summary>EnvVariable => value</summary>
    public Dictionary<string, string> Variables { get; set; } = [];
}

/// <summary>PUT /api/servers/{uuid}/rename</summary>
public class RenameServerRequest
{
    [Required, MinLength(1), MaxLength(64)]
    public string Name { get; set; } = "";

    [MaxLength(512)]
    public string? Description { get; set; }
}

/// <summary>POST /api/servers/{uuid}/console-ticket. Single use, valid for 30 seconds.</summary>
public class ConsoleTicketResponse
{
    public string Ticket { get; set; } = "";

    /// <summary>Relative websocket path: /api/servers/{uuid}/console?ticket=...</summary>
    public string Path { get; set; } = "";
}
