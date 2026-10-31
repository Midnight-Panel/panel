using Midnight.ApiServer.Features.Servers.Entities;

namespace Midnight.ApiServer.Features.Databases.Entities;

/// <summary>A database plus user on a database host, owned by a server</summary>
public class ServerDatabase
{
    public int Id { get; set; }

    public int ServerId { get; set; }
    public Server Server { get; set; } = null!;

    public int DatabaseHostId { get; set; }
    public DatabaseHost DatabaseHost { get; set; } = null!;

    /// <summary>Full name, s{serverId}_{suffix}</summary>
    public string Name { get; set; } = "";

    /// <summary>u{serverId}_{random}</summary>
    public string Username { get; set; } = "";

    /// <summary>Data protection encrypted password</summary>
    public string Password { get; set; } = "";

    /// <summary>Allowed connection source, "%" = anywhere</summary>
    public string Remote { get; set; } = "%";

    public DateTimeOffset CreatedAt { get; set; }
}
