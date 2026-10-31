using Midnight.Shared.Enums;
using Midnight.Shared.Http.Servers;
using Midnight.Shared.Http.Shards;

namespace Midnight.Shared.Http.Demon;

/// <summary>
/// Everything the demon needs to run a server. The panel is the source of truth: it sends this on create,
/// on every change (PUT) and the demon fetches all of them on boot (GET /api/nodes/remote/servers).
/// </summary>
public class DemonServerConfiguration
{
    public Guid Uuid { get; set; }

    /// <summary>For log messages only</summary>
    public string Name { get; set; } = "";

    public bool Suspended { get; set; }

    public string DockerImage { get; set; } = "";

    /// <summary>Startup command with all placeholders replaced, passed to the container as STARTUP environment variable</summary>
    public string StartupCommand { get; set; } = "";

    /// <summary>
    /// Environment of the container: the shard variables plus STARTUP, SERVER_MEMORY, SERVER_IP, SERVER_PORT,
    /// P_SERVER_UUID, P_SERVER_LOCATION and TZ (same names as other panels so existing images work)
    /// </summary>
    public Dictionary<string, string> Environment { get; set; } = [];

    public ServerLimitsDto Limits { get; set; } = new();

    public DemonAllocation PrimaryAllocation { get; set; } = new();

    /// <summary>All allocations including the primary one, every port is bound for tcp and udp</summary>
    public List<DemonAllocation> Allocations { get; set; } = [];

    public DemonProcessConfiguration Process { get; set; } = new();
    public DemonInstallConfiguration Install { get; set; } = new();
}

public class DemonAllocation
{
    public string IpAddress { get; set; } = "";
    public int Port { get; set; }
}

public class DemonProcessConfiguration
{
    /// <summary>Written to stdin to stop the server, null = send the stop signal</summary>
    public string? StopCommand { get; set; }

    /// <summary>e.g. SIGTERM, SIGINT, SIGKILL</summary>
    public string StopSignal { get; set; } = "SIGTERM";

    /// <summary>The server counts as online once a console line contains one of these, empty = online right after start</summary>
    public List<string> StartupDone { get; set; } = [];

    /// <summary>Files patched before every start, values already have their placeholders replaced</summary>
    public List<ShardConfigFileDto> ConfigFiles { get; set; } = [];

    public ShardFeaturesDto Features { get; set; } = new();
}

public class DemonInstallConfiguration
{
    /// <summary>null or empty = nothing to install</summary>
    public string? Script { get; set; }

    public string Image { get; set; } = "";
    public string Interpreter { get; set; } = "/bin/bash";
}

/// <summary>POST /api/servers on the demon</summary>
public class DemonCreateServerRequest
{
    public DemonServerConfiguration Configuration { get; set; } = new();

    public bool SkipInstall { get; set; }
    public bool StartOnCompletion { get; set; }
}

/// <summary>GET /api/servers/{uuid} and GET /api/servers (list) on the demon</summary>
public class DemonServerStateResponse
{
    public Guid Uuid { get; set; }
    public ServerState State { get; set; }
    public bool Installing { get; set; }
    public ServerStatsDto Stats { get; set; } = new();
}

/// <summary>GET /api/servers/{uuid}/logs on the demon: the last console lines</summary>
public class DemonConsoleLogResponse
{
    public List<string> Lines { get; set; } = [];
}

/// <summary>Demon → panel: POST /api/nodes/remote/servers/{uuid}/install</summary>
public class DemonInstallResultRequest
{
    public bool Successful { get; set; }
}
