using System.ComponentModel.DataAnnotations;

namespace Midnight.Shared.Http.Nodes;

public class NodeResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Fqdn { get; set; } = "";
    public int HttpPort { get; set; }
    public int SftpPort { get; set; }
    public bool UseSsl { get; set; }
    public int AllocationCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class NodeRequest
{
    [Required, MinLength(1), MaxLength(64)]
    public string Name { get; set; } = "";

    /// <summary>Hostname or ip address without scheme and port</summary>
    [Required, MaxLength(255), RegularExpression(@"^[a-zA-Z0-9]([a-zA-Z0-9.\-]*[a-zA-Z0-9])?$|^\[?[0-9a-fA-F:]+\]?$")]
    public string Fqdn { get; set; } = "";

    [Range(1, 65535)]
    public int HttpPort { get; set; } = 8080;

    [Range(1, 65535)]
    public int SftpPort { get; set; } = 2022;

    public bool UseSsl { get; set; }
}

/// <summary>Live state of a node, fetched from the demon</summary>
public class NodeStatusResponse
{
    public bool Online { get; set; }

    /// <summary>Why the demon could not be reached, null when online</summary>
    public string? Error { get; set; }

    public int? LatencyMs { get; set; }
    public Demon.SystemInfoResponse? System { get; set; }
}

public class NodeSetupResponse
{
    /// <summary>Content of /etc/midnight/config.json on the node</summary>
    public string Configuration { get; set; } = "";

    /// <summary>Shell command that writes the configuration file</summary>
    public string Command { get; set; } = "";
}

public class AllocationResponse
{
    public int Id { get; set; }
    public string IpAddress { get; set; } = "";
    public int Port { get; set; }
    public string? Notes { get; set; }
    public int? ServerId { get; set; }
}

public class CreateAllocationsRequest
{
    [Required, MaxLength(45)]
    public string IpAddress { get; set; } = "";

    /// <summary>Comma separated ports and ranges, e.g. "25565, 25570-25600"</summary>
    [Required, MaxLength(512)]
    public string Ports { get; set; } = "";

    [MaxLength(128)]
    public string? Notes { get; set; }
}

public class CreateAllocationsResponse
{
    public int Created { get; set; }

    /// <summary>Ports that already existed for this ip and were skipped</summary>
    public int Skipped { get; set; }
}

public class DeleteAllocationsRequest
{
    [MinLength(1)]
    public List<int> Ids { get; set; } = [];
}
