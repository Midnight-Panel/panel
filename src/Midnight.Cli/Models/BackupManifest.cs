namespace Midnight.Cli.Models;

public record BackupManifest
{
    public string Version { get; init; } = "1.0";
    public string PanelVersion { get; init; } = "unknown";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string DatabaseProvider { get; init; } = "PostgreSQL";
    public bool Encrypted { get; init; }
    public string? EncryptionAlgorithm { get; init; }
    public List<string> IncludedFiles { get; init; } = [];
    public string SystemArchitecture { get; init; } = "x86_64";
}

public record ContainerStatusInfo(
    string Name,
    string State,
    string Status,
    string Ports);

public record StatusReport
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string PanelStatus { get; set; } = "unknown";
    public string PanelVersion { get; set; } = "unknown";
    public string? PanelDomain { get; set; }
    public List<ContainerStatusInfo> Containers { get; set; } = [];
    public string DaemonStatus { get; set; } = "unknown";
    public string DaemonVersion { get; set; } = "unknown";
    public string DatabaseHealth { get; set; } = "unknown";
    public string DiskFreeSpace { get; set; } = "unknown";
    public string Architecture { get; set; } = "unknown";
}
