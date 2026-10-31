namespace Midnight.Shared.Http.Shards;

/// <summary>Kompakte Übersicht eines Shards für Listen</summary>
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

/// <summary>Ausführliche Detailansicht eines Shards mit Konfiguration und Variablen</summary>
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

    /// <summary>Verfügbare Docker-Images: Label => Image-Tag</summary>
    public Dictionary<string, string> DockerImages { get; set; } = [];
    public string DefaultDockerImage { get; set; } = "";

    /// <summary>Startbefehl mit Variablen-Platzhaltern z. B. java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}}</summary>
    public string StartupCommand { get; set; } = "";
    public string? StopCommand { get; set; }
    public string StopSignal { get; set; } = "SIGTERM";

    /// <summary>Automatisch beim Start gepatchte Konfigurationsdateien</summary>
    public List<ShardConfigFileDto> ConfigFiles { get; set; } = [];

    /// <summary>Erkennungsstring(s) für den erfolgreichen Server-Start (Startup-Done)</summary>
    public string StartupDone { get; set; } = "";

    /// <summary>Installationsskript für den Container</summary>
    public string? InstallScript { get; set; }
    public string InstallImage { get; set; } = "ghcr.io/midnightpanel/installers:alpine";
    public string InstallInterpreter { get; set; } = "/bin/bash";

    /// <summary>Zusatz-Features wie EULA, PID-Limits, Dateiverbote</summary>
    public ShardFeaturesDto Features { get; set; } = new();

    /// <summary>Zugeordnete Variablen</summary>
    public List<ShardVariableResponse> Variables { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Anfragemodell zum Anlegen oder Bearbeiten eines Shards</summary>
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
    public string InstallImage { get; set; } = "ghcr.io/midnightpanel/installers:alpine";
    public string InstallInterpreter { get; set; } = "/bin/bash";

    public ShardFeaturesDto Features { get; set; } = new();
}

/// <summary>Definition einer beim Serverstart modifizierten Konfigurationsdatei</summary>
public class ShardConfigFileDto
{
    public string Path { get; set; } = "";
    /// <summary>Parser-Typ: properties, yaml, json, ini, xml</summary>
    public string Parser { get; set; } = "properties";
    /// <summary>Schlüssel-zu-Wert/Platzhalter-Zuordnungen, z. B. "server-port" => "{{SERVER_PORT}}"</summary>
    public Dictionary<string, string> Mappings { get; set; } = [];
}

/// <summary>Spezifische Shard-Features</summary>
public class ShardFeaturesDto
{
    public bool Eula { get; set; }
    public int? PidLimit { get; set; }
    public List<string> FileDenylist { get; set; } = [];
}
