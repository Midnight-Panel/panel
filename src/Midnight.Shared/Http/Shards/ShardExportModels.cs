using System.Text.Json.Serialization;

namespace Midnight.Shared.Http.Shards;

/// <summary>Full export package of a shard for portability and versioning</summary>
public class ShardExportPackage
{
    public const string CurrentFormatVersion = "midnight_shard_v1";

    /// <summary>Fixed json name, the import detects the format by this property</summary>
    [JsonPropertyName("formatVersion")]
    public string FormatVersion { get; set; } = CurrentFormatVersion;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
    public ShardExportCollectionInfo Collection { get; set; } = new();
    public ShardExportData Shard { get; set; } = new();
}

public class ShardExportCollectionInfo
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Author { get; set; } = "";
}

public class ShardExportData
{
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

    public List<ShardVariableExportData> Variables { get; set; } = [];
}

public class ShardVariableExportData
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string EnvVariable { get; set; } = "";
    public string? DefaultValue { get; set; }
    public bool UserViewable { get; set; } = true;
    public bool UserEditable { get; set; } = true;
    public string Rules { get; set; } = "required|string";
    public int SortOrder { get; set; }
}

/// <summary>Request to import a shard (from Midnight format or legacy template format)</summary>
public class ShardImportRequest
{
    /// <summary>Optional collection ID if importing into an existing collection</summary>
    public int? TargetCollectionId { get; set; }

    /// <summary>JSON content of the export file</summary>
    public string Content { get; set; } = "";
}

/// <summary>Result after successful import</summary>
public class ShardImportResult
{
    public int ShardId { get; set; }
    public Guid ShardUuid { get; set; }
    public string ShardName { get; set; } = "";
    public int CollectionId { get; set; }
    public string CollectionName { get; set; } = "";
    public string DetectedFormat { get; set; } = "";
    public List<string> Warnings { get; set; } = [];
}

public class ShardBatchImportItem
{
    public string? FileName { get; set; }
    public string Content { get; set; } = "";
}

public class ShardBatchImportRequest
{
    public int? TargetCollectionId { get; set; }
    public List<ShardBatchImportItem> Items { get; set; } = [];
}

public class ShardBatchImportItemResult
{
    public string? FileName { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public ShardImportResult? Shard { get; set; }
}

public class ShardBatchImportResult
{
    public int TotalCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public List<ShardBatchImportItemResult> Results { get; set; } = [];
}
