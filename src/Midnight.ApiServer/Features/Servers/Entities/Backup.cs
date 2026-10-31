namespace Midnight.ApiServer.Features.Servers.Entities;

/// <summary>A tar.gz of the server files, stored by the demon in {dataPath}/backups/{serverUuid}/{uuid}.tar.gz</summary>
public class Backup
{
    public int Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();

    public int ServerId { get; set; }
    public Server Server { get; set; } = null!;

    public string Name { get; set; } = "";

    /// <summary>gitignore style lines</summary>
    public string IgnoredFiles { get; set; } = "";

    /// <summary>"sha256:hex", reported by the demon</summary>
    public string? Checksum { get; set; }

    public long Bytes { get; set; }
    public bool IsSuccessful { get; set; }

    /// <summary>Locked backups can't be deleted</summary>
    public bool IsLocked { get; set; }

    /// <summary>null while the demon is still creating the backup</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
