namespace Midnight.Shared.Protocol;

/// <summary>
/// Websocket messages of the server console. Every message is a json object {"event": "...", "data": ...}.
/// Browser ↔ panel (/api/servers/{uuid}/console?ticket=...) ↔ demon (/api/servers/{uuid}/ws, node token).
/// The panel checks permissions and forwards the messages unchanged in both directions.
/// Messages are serialized with JsonDefaults (camelCase properties, enums as names).
/// </summary>
public static class ConsoleEvents
{
    // ─── demon → browser ────────────────────────────────────────────────────

    /// <summary>data: string[] – the last console lines, sent once right after connecting</summary>
    public const string History = "history";

    /// <summary>data: string – one console line (may contain ANSI colors)</summary>
    public const string Console = "console";

    /// <summary>data: string – one line of the install script output</summary>
    public const string InstallOutput = "install-output";

    /// <summary>install-started: data null. install-completed: data bool, true when the script succeeded (a status event follows)</summary>
    public const string InstallStarted = "install-started";
    public const string InstallCompleted = "install-completed";

    /// <summary>data: ServerState name like JsonDefaults writes it ("Offline", "Starting", "Online", "Stopping", "Installing")</summary>
    public const string Status = "status";

    /// <summary>data: ServerStatsDto, about every 2 seconds while running</summary>
    public const string Stats = "stats";

    /// <summary>data: {"uuid": backup uuid, "successful": bool, "bytes": long} – a backup finished</summary>
    public const string BackupCompleted = "backup-completed";

    /// <summary>data: null – a backup restore started / finished (completed: {"successful": bool})</summary>
    public const string BackupRestoreStarted = "backup-restore-started";
    public const string BackupRestoreCompleted = "backup-restore-completed";

    /// <summary>data: string – something the user should see, e.g. "Disk limit exceeded"</summary>
    public const string Notice = "notice";

    /// <summary>data: string – a request of the browser failed</summary>
    public const string Error = "error";

    // ─── browser → demon ────────────────────────────────────────────────────

    /// <summary>data: string – written to the server's stdin</summary>
    public const string SendCommand = "command";

    /// <summary>data: PowerAction name, case insensitive ("Start", "Stop", "Restart", "Kill")</summary>
    public const string SetPower = "power";
}

public class ConsoleMessage
{
    public string Event { get; set; } = "";
    public object? Data { get; set; }
}
