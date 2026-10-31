namespace Midnight.ApiServer.Features.Nodes.Entities;

/// <summary>A machine running the Midnight daemon</summary>
public class Node
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Hostname or ip the panel and browsers use to reach the daemon</summary>
    public string Fqdn { get; set; } = "";

    public int HttpPort { get; set; } = 8080;
    public int SftpPort { get; set; } = 2022;
    public bool UseSsl { get; set; }

    /// <summary>Public part of the node token, used to look up the node</summary>
    public string TokenId { get; set; } = "";

    /// <summary>Data protection encrypted secret part of the node token</summary>
    public string Token { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public List<Allocation> Allocations { get; set; } = [];
}
