using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Midnight.ApiServer.Core.Configuration;

/// <summary>
/// Creates storage/config.json on first start and fills in generated secrets,
/// before the configuration system reads it.
/// </summary>
public static class ConfigurationBootstrapper
{
    public const string ConfigFileName = "config.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string GetStoragePath(string contentRoot)
    {
        var path = Environment.GetEnvironmentVariable("MIDNIGHT_STORAGE");

        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(contentRoot, "storage");

        Directory.CreateDirectory(path);
        return Path.GetFullPath(path);
    }

    public static string EnsureConfigFile(string storagePath)
    {
        var configPath = Path.Combine(storagePath, ConfigFileName);

        JsonObject root;

        if (File.Exists(configPath))
            root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject ?? new JsonObject();
        else
            root = JsonSerializer.SerializeToNode(new AppConfiguration(), JsonOptions)!.AsObject();

        var changed = !File.Exists(configPath);

        if (root["Security"] is not JsonObject security)
        {
            security = new JsonObject();
            root["Security"] = security;
        }

        var secret = security["JwtSecret"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            security["JwtSecret"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            changed = true;
        }

        if (changed)
        {
            File.WriteAllText(configPath, root.ToJsonString(JsonOptions));

            // The file contains secrets, keep it private to the panel user
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(configPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return configPath;
    }
}
