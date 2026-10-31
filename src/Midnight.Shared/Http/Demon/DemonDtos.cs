namespace Midnight.Shared.Http.Demon;

/// <summary>Returned by the demon on GET /api/system/info</summary>
public class SystemInfoResponse
{
    public string DemonVersion { get; set; } = "";
    public int ProtocolVersion { get; set; }
    public string OperatingSystem { get; set; } = "";
    public string KernelVersion { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string Hostname { get; set; } = "";
    public TimeSpan Uptime { get; set; }

    public int CpuCores { get; set; }
    public string? CpuModel { get; set; }

    /// <summary>System wide cpu usage in percent (0-100)</summary>
    public double CpuUsage { get; set; }

    public long MemoryTotalBytes { get; set; }
    public long MemoryUsedBytes { get; set; }

    /// <summary>Disk of the data path</summary>
    public long DiskTotalBytes { get; set; }
    public long DiskUsedBytes { get; set; }

    public DockerInfo Docker { get; set; } = new();
}

public class DockerInfo
{
    public bool Available { get; set; }
    public string? Version { get; set; }
    public string? Error { get; set; }
    public int Containers { get; set; }
    public int ContainersRunning { get; set; }
    public int Images { get; set; }
}

/// <summary>Returned by the demon on GET /api/system/logs</summary>
public class DemonLogsResponse
{
    public List<string> Lines { get; set; } = [];
}

/// <summary>Returned by the panel on GET /api/nodes/remote/boot</summary>
public class BootResponse
{
    public string NodeName { get; set; } = "";

    /// <summary>Uuid and name only, the full configurations come from GET /api/nodes/remote/servers</summary>
    public List<BootServer> Servers { get; set; } = [];
}

public class BootServer
{
    public Guid Uuid { get; set; }
    public string Name { get; set; } = "";
}
