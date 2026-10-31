using System.ComponentModel.DataAnnotations;

namespace Midnight.Shared.Http.Admin;

/// <summary>A MySQL / MariaDB server the panel creates server databases on. /api/admin/database-hosts</summary>
public class DatabaseHostResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Username { get; set; } = "";

    /// <summary>Only servers on this node use the host, null = all nodes</summary>
    public int? NodeId { get; set; }

    public string? NodeName { get; set; }
    public int? MaxDatabases { get; set; }
    public int DatabaseCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class DatabaseHostRequest
{
    [Required, MinLength(1), MaxLength(64)]
    public string Name { get; set; } = "";

    [Required, MaxLength(255)]
    public string Host { get; set; } = "";

    [Range(1, 65535)]
    public int Port { get; set; } = 3306;

    /// <summary>Needs CREATE USER and GRANT OPTION</summary>
    [Required, MaxLength(64)]
    public string Username { get; set; } = "";

    /// <summary>Required on create, null on update keeps the stored password</summary>
    [MaxLength(255)]
    public string? Password { get; set; }

    public int? NodeId { get; set; }

    [Range(1, 100_000)]
    public int? MaxDatabases { get; set; }
}

public class DatabaseHostTestResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Version { get; set; }
}
