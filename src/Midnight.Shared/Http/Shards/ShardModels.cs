namespace Midnight.Shared.Http.Shards;

/// <summary>Compact overview of a shard for lists</summary>
public class ShardSummaryResponse
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public int CollectionId { get; set; }
    public string CollectionName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string DefaultDockerImage { get; set; } = "";
    public int VariableCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Detailed view of a shard with full configuration and variables</summary>
public class ShardDetailResponse
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public int CollectionId { get; set; }
    public string CollectionName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";

    /// <summary>Available container images: Label => Image tag</summary>
    public Dictionary<string, string> DockerImages { get; set; } = [];
    public string DefaultDockerImage { get; set; } = "";

    /// <summary>Startup command with variable placeholders e.g. java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}}</summary>
    public string StartupCommand { get; set; } = "";
    public string? StopCommand { get; set; }
    public string StopSignal { get; set; } = "SIGTERM";

    /// <summary>Configuration files patched automatically on startup</summary>
    public List<ShardConfigFileDto> ConfigFiles { get; set; } = [];

    /// <summary>Detection string(s) for successful server boot (Startup-Done)</summary>
    public string StartupDone { get; set; } = "";

    /// <summary>Installation script for container</summary>
    public string? InstallScript { get; set; }
    public string InstallImage { get; set; } = "ghcr.io/pterodactyl/installers:alpine";
    public string InstallInterpreter { get; set; } = "/bin/bash";

    /// <summary>Additional features like EULA, PID limits, file denylists</summary>
    public ShardFeaturesDto Features { get; set; } = new();

    /// <summary>Assigned variables</summary>
    public List<ShardVariableResponse> Variables { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Request model to create or update a shard</summary>
public class ShardRequest
{
    public int CollectionId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";

    public Dictionary<string, string> DockerImages { get; set; } = [];
    public string DefaultDockerImage { get; set; } = "";

    public string StartupCommand { get; set; } = "";
    public string? StopCommand { get; set; }
    public string StopSignal { get; set; } = "SIGTERM";

    public List<ShardConfigFileDto> ConfigFiles { get; set; } = [];
    public string StartupDone { get; set; } = "";

    public string? InstallScript { get; set; }
    public string InstallImage { get; set; } = "ghcr.io/pterodactyl/installers:alpine";
    public string InstallInterpreter { get; set; } = "/bin/bash";

    public ShardFeaturesDto Features { get; set; } = new();
}

/// <summary>Definition of a configuration file patched on server startup</summary>
public class ShardConfigFileDto
{
    public string Path { get; set; } = "";
    /// <summary>Parser type: properties, yaml, json, ini, xml</summary>
    public string Parser { get; set; } = "properties";
    /// <summary>Key to value/placeholder mappings, e.g. "server-port" => "{{SERVER_PORT}}"</summary>
    public Dictionary<string, string> Mappings { get; set; } = [];
}

/// <summary>Specific shard features</summary>
public class ShardFeaturesDto
{
    public bool Eula { get; set; }
    public int? PidLimit { get; set; }
    public List<string> FileDenylist { get; set; } = [];
}
