using System.ComponentModel.DataAnnotations;

namespace Midnight.Shared.Http.Servers;

// ─── Subusers: /api/servers/{uuid}/users ────────────────────────────────────

public class SubuserResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public bool HasAvatar { get; set; }
    public List<string> Permissions { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateSubuserRequest
{
    /// <summary>Email of an existing panel account</summary>
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = "";

    [Required]
    public List<string> Permissions { get; set; } = [];
}

public class UpdateSubuserRequest
{
    [Required]
    public List<string> Permissions { get; set; } = [];
}

// ─── Files: /api/servers/{uuid}/files/... ───────────────────────────────────

public class FileEntryResponse
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public bool IsDirectory { get; set; }
    public bool IsSymlink { get; set; }

    /// <summary>"-rw-r--r--"</summary>
    public string Mode { get; set; } = "";

    /// <summary>"644"</summary>
    public string ModeBits { get; set; } = "";

    public string MimeType { get; set; } = "";

    /// <summary>Text file small enough for the editor</summary>
    public bool IsEditable { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }
}

/// <summary>Paths are always relative to the server root, "/" = the root. ".." can never leave it.</summary>
public class FileRootRequest
{
    [Required, MaxLength(4096)]
    public string Root { get; set; } = "/";
}

public class CreateFolderRequest : FileRootRequest
{
    [Required, MinLength(1), MaxLength(255)]
    public string Name { get; set; } = "";
}

public class RenameFilesRequest : FileRootRequest
{
    /// <summary>From/To relative to Root, To may contain folders ("../x" moves up)</summary>
    [Required, MinLength(1)]
    public List<FileRename> Files { get; set; } = [];
}

public class FileRename
{
    [Required, MaxLength(4096)]
    public string From { get; set; } = "";

    [Required, MaxLength(4096)]
    public string To { get; set; } = "";
}

public class CopyFileRequest
{
    /// <summary>File to copy, the copy is named "name copy.ext" next to it</summary>
    [Required, MaxLength(4096)]
    public string Location { get; set; } = "";
}

public class DeleteFilesRequest : FileRootRequest
{
    [Required, MinLength(1)]
    public List<string> Files { get; set; } = [];
}

public class CompressFilesRequest : FileRootRequest
{
    [Required, MinLength(1)]
    public List<string> Files { get; set; } = [];

    /// <summary>"tar.gz" or "zip"</summary>
    [RegularExpression("^(tar\\.gz|zip)$")]
    public string Format { get; set; } = "tar.gz";
}

public class DecompressFileRequest : FileRootRequest
{
    [Required, MaxLength(4096)]
    public string File { get; set; } = "";
}

public class ChmodFilesRequest : FileRootRequest
{
    [Required, MinLength(1)]
    public List<FileChmod> Files { get; set; } = [];
}

public class FileChmod
{
    [Required, MaxLength(4096)]
    public string File { get; set; } = "";

    /// <summary>Octal like "644" or "755"</summary>
    [Required, RegularExpression("^[0-7]{3,4}$")]
    public string Mode { get; set; } = "644";
}

public class PullFileRequest
{
    [Required, Url, MaxLength(2048)]
    public string Url { get; set; } = "";

    [Required, MaxLength(4096)]
    public string Directory { get; set; } = "/";

    /// <summary>null = name from the url / Content-Disposition</summary>
    [MaxLength(255)]
    public string? FileName { get; set; }
}

// ─── Backups: /api/servers/{uuid}/backups ───────────────────────────────────

public class BackupResponse
{
    public Guid Uuid { get; set; }
    public string Name { get; set; } = "";

    /// <summary>gitignore style lines</summary>
    public string IgnoredFiles { get; set; } = "";

    public string? Checksum { get; set; }
    public long Bytes { get; set; }
    public bool IsSuccessful { get; set; }
    public bool IsLocked { get; set; }

    /// <summary>null while the backup is still running</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public class BackupListResponse
{
    public List<BackupResponse> Backups { get; set; } = [];

    /// <summary>FeatureLimits.Backups of the server</summary>
    public int Limit { get; set; }
}

public class CreateBackupRequest
{
    /// <summary>null = "Backup at {date}"</summary>
    [MaxLength(191)]
    public string? Name { get; set; }

    [MaxLength(8192)]
    public string? IgnoredFiles { get; set; }

    public bool IsLocked { get; set; }
}

public class RestoreBackupRequest
{
    /// <summary>Delete all files before restoring</summary>
    public bool Truncate { get; set; }
}

// ─── Network: /api/servers/{uuid}/allocations ───────────────────────────────

public class ServerAllocationListResponse
{
    public List<ServerAllocationDto> Allocations { get; set; } = [];

    /// <summary>How many allocations besides the primary one the server may have</summary>
    public int Limit { get; set; }
}

public class UpdateAllocationNotesRequest
{
    [MaxLength(255)]
    public string? Notes { get; set; }
}

// ─── Schedules: /api/servers/{uuid}/schedules ───────────────────────────────

public enum ScheduleTaskAction
{
    Command = 0,
    Power = 1,
    Backup = 2
}

public class ScheduleResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Cron parts, each like "*", "*/5", "0", "1-5"</summary>
    public string Minute { get; set; } = "*";
    public string Hour { get; set; } = "*";
    public string DayOfMonth { get; set; } = "*";
    public string Month { get; set; } = "*";
    public string DayOfWeek { get; set; } = "*";

    public bool IsActive { get; set; }

    /// <summary>Skip the run when the server is not online</summary>
    public bool OnlyWhenOnline { get; set; }

    public bool IsProcessing { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public List<ScheduleTaskResponse> Tasks { get; set; } = [];
}

public class ScheduleTaskResponse
{
    public int Id { get; set; }
    public int Sequence { get; set; }
    public ScheduleTaskAction Action { get; set; }

    /// <summary>Command: the command. Power: Start/Stop/Restart/Kill. Backup: ignored files (optional)</summary>
    public string Payload { get; set; } = "";

    /// <summary>Seconds to wait after the previous task</summary>
    public int TimeOffset { get; set; }

    public bool ContinueOnFailure { get; set; }
}

public class ScheduleRequest
{
    [Required, MinLength(1), MaxLength(191)]
    public string Name { get; set; } = "";

    [Required, MaxLength(64)] public string Minute { get; set; } = "*";
    [Required, MaxLength(64)] public string Hour { get; set; } = "*";
    [Required, MaxLength(64)] public string DayOfMonth { get; set; } = "*";
    [Required, MaxLength(64)] public string Month { get; set; } = "*";
    [Required, MaxLength(64)] public string DayOfWeek { get; set; } = "*";

    public bool IsActive { get; set; } = true;
    public bool OnlyWhenOnline { get; set; }
}

public class ScheduleTaskRequest
{
    public ScheduleTaskAction Action { get; set; }

    [MaxLength(2048)]
    public string Payload { get; set; } = "";

    [Range(0, 900)]
    public int TimeOffset { get; set; }

    public bool ContinueOnFailure { get; set; }
}

// ─── Databases: /api/servers/{uuid}/databases ───────────────────────────────

public class ServerDatabaseResponse
{
    public int Id { get; set; }

    /// <summary>Full database name, e.g. s12_minecraft</summary>
    public string Name { get; set; } = "";

    public string Username { get; set; } = "";

    /// <summary>Only with database.view-password, otherwise null</summary>
    public string? Password { get; set; }

    /// <summary>Allowed connection source, "%" = anywhere</summary>
    public string Remote { get; set; } = "%";

    public string Host { get; set; } = "";
    public int Port { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ServerDatabaseListResponse
{
    public List<ServerDatabaseResponse> Databases { get; set; } = [];
    public int Limit { get; set; }
}

public class CreateServerDatabaseRequest
{
    /// <summary>Suffix, the panel prefixes it with s{serverId}_</summary>
    [Required, MinLength(1), MaxLength(48), RegularExpression("^[a-zA-Z0-9_]+$")]
    public string Database { get; set; } = "";

    [Required, MaxLength(64), RegularExpression(@"^[0-9a-zA-Z.%:_\-/]+$")]
    public string Remote { get; set; } = "%";
}

// ─── Activity: /api/servers/{uuid}/activity ─────────────────────────────────

public class ServerActivityResponse
{
    public long Id { get; set; }

    /// <summary>e.g. "server:power.start", "server:file.write", "server:subuser.create"</summary>
    public string Event { get; set; } = "";

    /// <summary>Short context like file names or the command, already safe to show</summary>
    public Dictionary<string, string> Properties { get; set; } = [];

    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// ─── Downloads ──────────────────────────────────────────────────────────────

/// <summary>
/// POST …/files/download-ticket?file= and POST …/backups/{backupUuid}/download-ticket.
/// Single use, valid for 60 seconds, plain link without the bearer token.
/// </summary>
public class DownloadTicketResponse
{
    /// <summary>Relative url, e.g. /api/servers/{uuid}/files/download?ticket=…</summary>
    public string Url { get; set; } = "";
}
