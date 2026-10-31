namespace Midnight.Cli.Common;

/// <summary>
/// Holds global CLI parameters and runtime options such as --dry-run and -y.
/// </summary>
public record CliContext
{
    public bool DryRun { get; init; }
    public bool Yes { get; init; }
    public bool Debug { get; init; }
    public string LogFilePath { get; init; } = "/var/log/midnight-installer.log";

    public static CliContext Default => new();
}
