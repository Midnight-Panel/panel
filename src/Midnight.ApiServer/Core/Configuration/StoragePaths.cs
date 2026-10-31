namespace Midnight.ApiServer.Core.Configuration;

/// <summary>Absolute path of the storage directory (config, sqlite database, data protection keys, avatars, logs)</summary>
public record StoragePaths(string Root)
{
    public string Logs => Path.Combine(Root, "logs");
    public string Keys => Path.Combine(Root, "keys");
    public string Branding => Path.Combine(Root, "branding");
    public string Extensions => Path.Combine(Root, "extensions");
    public string ExtensionPackages => Path.Combine(Extensions, "packages");
    public string ExtensionInstalled => Path.Combine(Extensions, "installed");
}
