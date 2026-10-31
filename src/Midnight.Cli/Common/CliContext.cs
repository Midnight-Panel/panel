namespace Midnight.Cli.Common;

/// <summary>
/// Hält globale CLI-Parameter und Laufzeitoptionen wie --dry-run und -y.
/// </summary>
public record CliContext
{
    public bool DryRun { get; init; }
    public bool Yes { get; init; }
    public bool Debug { get; init; }
    public string LogFilePath { get; init; } = "/var/log/midnight-installer.log";

    public static CliContext Default => new();
}
