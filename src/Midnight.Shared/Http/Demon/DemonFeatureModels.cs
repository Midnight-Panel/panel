namespace Midnight.Shared.Http.Demon;

/// <summary>Panel → demon: POST /api/servers/{uuid}/backups</summary>
public class DemonCreateBackupRequest
{
    public Guid BackupUuid { get; set; }

    /// <summary>gitignore style lines, relative to the server root</summary>
    public string IgnoredFiles { get; set; } = "";
}

/// <summary>Panel → demon: POST /api/servers/{uuid}/backups/{backupUuid}/restore</summary>
public class DemonRestoreBackupRequest
{
    public bool Truncate { get; set; }
}

/// <summary>Demon → panel: POST /api/nodes/remote/backups/{backupUuid}</summary>
public class DemonBackupResultRequest
{
    public bool Successful { get; set; }

    /// <summary>"sha256:hex"</summary>
    public string? Checksum { get; set; }

    public long Bytes { get; set; }
}

/// <summary>Demon → panel: POST /api/nodes/remote/backups/{backupUuid}/restore</summary>
public class DemonRestoreResultRequest
{
    public bool Successful { get; set; }
}
