using System.Text.Json;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Entities;

/// <summary>
/// Vorlage für einen Game- oder Anwendungs-Server (z. B. Paper, Rust, CS2, Node.js Bot).
/// Definiert Startparameter, Docker-Images, Umgebungsvariablen und Skripte.
/// </summary>
public class Shard
{
    public int Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();

    public int CollectionId { get; set; }
    public ShardCollection Collection { get; set; } = null!;

    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";

    /// <summary>Serialisiertes Wörterbuch: Label => Docker-Image</summary>
    public string DockerImagesJson { get; set; } = "{}";
    public string DefaultDockerImage { get; set; } = "";

    /// <summary>Startbefehl mit Platzhaltern wie {{SERVER_MEMORY}} und Variablen wie {{SERVER_JARFILE}}</summary>
    public string StartupCommand { get; set; } = "";
    public string? StopCommand { get; set; }
    public string StopSignal { get; set; } = "SIGTERM";

    /// <summary>Serialisierte Liste von Konfigurationsdatei-Patches</summary>
    public string ConfigFilesJson { get; set; } = "[]";

    /// <summary>Erkennungsstring(s) für erfolgreichen Start (Startup-Done)</summary>
    public string StartupDone { get; set; } = "";

    /// <summary>Installationsskript, Container-Image und Interpreter</summary>
    public string? InstallScript { get; set; }
    public string InstallImage { get; set; } = "ghcr.io/midnightpanel/installers:alpine";
    public string InstallInterpreter { get; set; } = "/bin/bash";

    /// <summary>Zusatz-Features (EULA, PidLimit, FileDenylist) im JSON-Format</summary>
    public string FeaturesJson { get; set; } = "{}";

    public ICollection<ShardVariable> Variables { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Hilfsmethoden für JSON-Typen
    public Dictionary<string, string> GetDockerImages()
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(DockerImagesJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SetDockerImages(Dictionary<string, string> images)
    {
        DockerImagesJson = JsonSerializer.Serialize(images ?? []);
    }

    public List<ShardConfigFileDto> GetConfigFiles()
    {
        try
        {
            return JsonSerializer.Deserialize<List<ShardConfigFileDto>>(ConfigFilesJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SetConfigFiles(List<ShardConfigFileDto> configs)
    {
        ConfigFilesJson = JsonSerializer.Serialize(configs ?? []);
    }

    public ShardFeaturesDto GetFeatures()
    {
        try
        {
            return JsonSerializer.Deserialize<ShardFeaturesDto>(FeaturesJson) ?? new ShardFeaturesDto();
        }
        catch
        {
            return new ShardFeaturesDto();
        }
    }

    public void SetFeatures(ShardFeaturesDto features)
    {
        FeaturesJson = JsonSerializer.Serialize(features ?? new ShardFeaturesDto());
    }
}
