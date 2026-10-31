using Midnight.ApiServer.Features.Nodes.Entities;

namespace Midnight.ApiServer.Features.Databases.Entities;

/// <summary>A MySQL / MariaDB server the panel creates server databases on</summary>
public class DatabaseHost
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 3306;
    public string Username { get; set; } = "";

    /// <summary>Data protection encrypted password</summary>
    public string Password { get; set; } = "";

    /// <summary>Only servers on this node use the host, null = all nodes</summary>
    public int? NodeId { get; set; }
    public Node? Node { get; set; }

    public int? MaxDatabases { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<ServerDatabase> Databases { get; set; } = [];
}
