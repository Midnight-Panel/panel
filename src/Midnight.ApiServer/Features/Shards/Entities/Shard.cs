using System.Text.Json;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Entities;

/// <summary>
/// Template for a game or application server (e.g. Paper, Rust, CS2, Node.js Bot).
/// Defines startup parameters, container images, environment variables, and install scripts.
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

    /// <summary>Serialized dictionary: Label => Docker image URI</summary>
    public string DockerImagesJson { get; set; } = "{}";
    public string DefaultDockerImage { get; set; } = "";

    /// <summary>Startup command with placeholders like {{SERVER_MEMORY}} and {{SERVER_JARFILE}}</summary>
    public string StartupCommand { get; set; } = "";
    public string? StopCommand { get; set; }
    public string StopSignal { get; set; } = "SIGTERM";

    /// <summary>Serialized list of configuration file patches</summary>
    public string ConfigFilesJson { get; set; } = "[]";

    /// <summary>Detection string(s) for successful server boot (Startup-Done)</summary>
    public string StartupDone { get; set; } = "";

    /// <summary>Installation script, container image, and interpreter</summary>
    public string? InstallScript { get; set; }
    public string InstallImage { get; set; } = "ghcr.io/pterodactyl/installers:alpine";
    public string InstallInterpreter { get; set; } = "/bin/bash";

    /// <summary>Additional features (EULA, PidLimit, FileDenylist) in JSON format</summary>
    public string FeaturesJson { get; set; } = "{}";

    public ICollection<ShardVariable> Variables { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Helper methods for JSON-serialized properties
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
