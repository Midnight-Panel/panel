namespace Midnight.Cli.Models;

/// <summary>
/// Persistent installation state in /etc/midnight/midnight.json.
/// </summary>
public record InstallationState
{
    public bool PanelInstalled { get; set; }
    public bool DaemonInstalled { get; set; }
    public string PanelVersion { get; set; } = "v0.1.0";
    public string DaemonVersion { get; set; } = "v0.1.0";
    public string? PreviousPanelVersion { get; set; }
    public string? PreviousDaemonVersion { get; set; }
    public string? LastBackupPath { get; set; }
    public bool UseImages { get; set; }
    public string PanelDomain { get; set; } = "localhost";
    public int PanelPort { get; set; } = 80;
    public int DaemonPort { get; set; } = 8088;
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
