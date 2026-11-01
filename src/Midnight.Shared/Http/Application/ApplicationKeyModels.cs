using System.ComponentModel.DataAnnotations;

namespace Midnight.Shared.Http.Application;

/// <summary>Areas an application api key can access. Stored by name, serialized as name ("Servers").</summary>
public enum ApplicationResource
{
    Users = 0,
    Servers = 1,
    Nodes = 2,
    Allocations = 3,
    Shards = 4,
    DatabaseHosts = 5
}

public enum ApplicationAccess
{
    None = 0,
    Read = 1,
    ReadWrite = 2
}

/// <summary>
/// An application api key ("mda_{identifier}_{secret}") for automation, e.g. billing systems.
/// Managed under /api/admin/application-keys, used against /api/application/v1/...
/// </summary>
public class ApplicationKeyResponse
{
    public int Id { get; set; }

    /// <summary>Public part of the key ("mda_ab12cd34"), the secret is never shown again</summary>
    public string Identifier { get; set; } = "";

    public string Description { get; set; } = "";

    /// <summary>Every resource is present, missing ones in requests count as None</summary>
    public Dictionary<ApplicationResource, ApplicationAccess> Permissions { get; set; } = [];

    /// <summary>IPs or CIDR ranges the key may be used from, empty = everywhere</summary>
    public List<string> AllowedIps { get; set; } = [];

    public int? CreatedById { get; set; }
    public string? CreatedByUsername { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

public class ApplicationKeyRequest
{
    [Required, MinLength(1), MaxLength(255)]
    public string Description { get; set; } = "";

    public Dictionary<ApplicationResource, ApplicationAccess> Permissions { get; set; } = [];

    /// <summary>Max 32 entries, each an IPv4/IPv6 address or CIDR range</summary>
    [MaxLength(32)]
    public List<string> AllowedIps { get; set; } = [];
}

/// <summary>Returned once on creation, the full token can't be read again</summary>
public class CreatedApplicationKeyResponse
{
    public ApplicationKeyResponse Key { get; set; } = new();

    /// <summary>"mda_{identifier}_{secret}"</summary>
    public string Token { get; set; } = "";
}
