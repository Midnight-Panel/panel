using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Services;

public partial class ShardImportExportService : IShardImportExportService
{
    [GeneratedRegex(@"^[A-Z_][A-Z0-9_]*$")]
    private static partial Regex EnvVariableRegex();

    private readonly DataContext _context;
    private readonly IShardService _shardService;
    private readonly IShardCollectionService _collectionService;
    private readonly TimeProvider _timeProvider;

    public ShardImportExportService(
        DataContext context,
        IShardService shardService,
        IShardCollectionService collectionService,
        TimeProvider timeProvider)
    {
        _context = context;
        _shardService = shardService;
        _collectionService = collectionService;
        _timeProvider = timeProvider;
    }

    public async Task<ShardExportPackage> ExportAsync(int shardId, CancellationToken cancellationToken)
    {
        var shard = await _context.Shards
            .Include(x => x.Collection)
            .Include(x => x.Variables)
            .FirstOrDefaultAsync(x => x.Id == shardId, cancellationToken)
            ?? throw HttpApiException.NotFound("Shard not found");

        return new ShardExportPackage
        {
            FormatVersion = ShardExportPackage.CurrentFormatVersion,
            ExportedAt = _timeProvider.GetUtcNow(),
            Collection = new ShardExportCollectionInfo
            {
                Name = shard.Collection.Name,
                Description = shard.Collection.Description,
                Author = shard.Collection.Author
            },
            Shard = new ShardExportData
            {
                Name = shard.Name,
                Description = shard.Description,
                Author = shard.Author,
                Version = shard.Version,
                DockerImages = shard.GetDockerImages(),
                DefaultDockerImage = shard.DefaultDockerImage,
                StartupCommand = shard.StartupCommand,
                StopCommand = shard.StopCommand,
                StopSignal = shard.StopSignal,
                ConfigFiles = shard.GetConfigFiles(),
                StartupDone = shard.StartupDone,
                InstallScript = shard.InstallScript,
                InstallImage = shard.InstallImage,
                InstallInterpreter = shard.InstallInterpreter,
                Features = shard.GetFeatures(),
                Variables = shard.Variables.OrderBy(v => v.SortOrder).Select(v => new ShardVariableExportData
                {
                    Name = v.Name,
                    Description = v.Description,
                    EnvVariable = v.EnvVariable,
                    DefaultValue = v.DefaultValue,
                    UserViewable = v.UserViewable,
                    UserEditable = v.UserEditable,
                    Rules = v.Rules,
                    SortOrder = v.SortOrder
                }).ToList()
            }
        };
    }

    public async Task<ShardImportResult> ImportAsync(ShardImportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw HttpApiException.BadRequest("Import content is empty.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(request.Content);
        }
        catch (JsonException ex)
        {
            throw HttpApiException.BadRequest("Invalid JSON format", ex.Message);
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                throw HttpApiException.BadRequest("Unknown file format", "The JSON file could not be identified as a Midnight shard or legacy template.");

            if (request.TargetCollectionId.HasValue &&
                !await _context.ShardCollections.AnyAsync(x => x.Id == request.TargetCollectionId.Value, cancellationToken))
                throw HttpApiException.NotFound("Shard collection not found");

            try
            {
                // Format detection: Native Midnightpanel format
                if ((root.TryGetProperty("formatVersion", out var fvProp) || root.TryGetProperty("FormatVersion", out fvProp)) &&
                    fvProp.ValueKind == JsonValueKind.String && fvProp.GetString() == ShardExportPackage.CurrentFormatVersion)
                {
                    return await ImportMidnightFormatAsync(root, request.TargetCollectionId, cancellationToken);
                }

                // Format detection: Legacy PTDL format
                if (root.TryGetProperty("meta", out _) || root.TryGetProperty("_comment", out _) || root.TryGetProperty("exported_at", out _))
                {
                    return await ImportPtdlLegacyFormatAsync(root, request.TargetCollectionId, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // Properties with unexpected json types (e.g. a number instead of a string)
                throw HttpApiException.BadRequest("Invalid shard file", ex.Message);
            }

            throw HttpApiException.BadRequest("Unknown file format", "The JSON file could not be identified as a Midnight shard or legacy template.");
        }
    }

    private async Task<ShardImportResult> ImportMidnightFormatAsync(JsonElement root, int? targetCollectionId, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var package = root.Deserialize<ShardExportPackage>(options)
                      ?? throw HttpApiException.BadRequest("Failed to deserialize shard package.");

        if (package.Shard == null || string.IsNullOrWhiteSpace(package.Shard.Name))
            throw HttpApiException.BadRequest("Invalid shard file", "The package does not contain a shard with a name.");

        var collectionInfo = package.Collection ?? new ShardExportCollectionInfo();
        var collectionName = string.IsNullOrWhiteSpace(collectionInfo.Name) ? "Imported" : collectionInfo.Name.Trim();

        var collectionId = targetCollectionId ?? await EnsureCollectionAsync(collectionName, collectionInfo.Description, collectionInfo.Author, cancellationToken);
        var collection = await _context.ShardCollections.FirstAsync(x => x.Id == collectionId, cancellationToken);

        // Disambiguate name if already present in collection
        var uniqueName = await GetUniqueShardNameAsync(collectionId, package.Shard.Name, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var shard = new Shard
        {
            Uuid = Guid.NewGuid(),
            CollectionId = collectionId,
            Name = uniqueName,
            Description = package.Shard.Description,
            Author = package.Shard.Author,
            Version = package.Shard.Version,
            DefaultDockerImage = package.Shard.DefaultDockerImage,
            StartupCommand = package.Shard.StartupCommand,
            StopCommand = package.Shard.StopCommand,
            StopSignal = package.Shard.StopSignal,
            StartupDone = package.Shard.StartupDone,
            InstallScript = package.Shard.InstallScript,
            InstallImage = package.Shard.InstallImage,
            InstallInterpreter = package.Shard.InstallInterpreter,
            CreatedAt = now,
            UpdatedAt = now
        };

        shard.SetDockerImages(package.Shard.DockerImages);
        shard.SetConfigFiles(package.Shard.ConfigFiles);
        shard.SetFeatures(package.Shard.Features);

        // Variables are added through the navigation so shard and variables are saved in one step
        var sort = 0;
        var envNames = new HashSet<string>();
        foreach (var v in package.Shard.Variables ?? [])
        {
            if (v == null)
                continue;

            shard.Variables.Add(new ShardVariable
            {
                Name = string.IsNullOrWhiteSpace(v.Name) ? "Variable" : v.Name,
                Description = v.Description,
                EnvVariable = NormalizeEnvVariable(v.EnvVariable, envNames),
                DefaultValue = v.DefaultValue,
                UserViewable = v.UserViewable,
                UserEditable = v.UserEditable,
                Rules = string.IsNullOrWhiteSpace(v.Rules) ? "required|string" : v.Rules,
                SortOrder = v.SortOrder > 0 ? v.SortOrder : ++sort
            });
        }

        _context.Shards.Add(shard);
        await _context.SaveChangesAsync(cancellationToken);

        return new ShardImportResult
        {
            ShardId = shard.Id,
            ShardUuid = shard.Uuid,
            ShardName = shard.Name,
            CollectionId = collection.Id,
            CollectionName = collection.Name,
            DetectedFormat = "Midnight Shard (v1)"
        };
    }

    private async Task<ShardImportResult> ImportPtdlLegacyFormatAsync(JsonElement root, int? targetCollectionId, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "Imported Shard" : "Imported Shard";
        var description = root.TryGetProperty("description", out var d) ? d.GetString() : null;
        var author = root.TryGetProperty("author", out var a) ? a.GetString() ?? "Community" : "Community";

        var collectionId = targetCollectionId ?? await EnsureCollectionAsync("Imported", "Collection for imported templates", "Midnightpanel", cancellationToken);
        var collection = await _context.ShardCollections.FirstAsync(x => x.Id == collectionId, cancellationToken);
        var uniqueName = await GetUniqueShardNameAsync(collectionId, name, cancellationToken);

        // Read Docker images
        var images = new Dictionary<string, string>();
        var defaultImage = "";
        if (root.TryGetProperty("images", out var imgObj) && imgObj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in imgObj.EnumerateObject())
            {
                images[prop.Name] = prop.Value.GetString() ?? "";
            }
        }
        else if (root.TryGetProperty("docker_images", out var dimgObj) && dimgObj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in dimgObj.EnumerateObject())
            {
                images[prop.Name] = prop.Value.GetString() ?? "";
            }
        }

        if (root.TryGetProperty("image", out var singleImg) && singleImg.ValueKind == JsonValueKind.String)
        {
            var imgStr = singleImg.GetString() ?? "";
            if (!string.IsNullOrEmpty(imgStr))
            {
                images["Default"] = imgStr;
                defaultImage = imgStr;
            }
        }

        if (string.IsNullOrEmpty(defaultImage) && images.Count > 0)
        {
            defaultImage = images.Values.First();
        }

        // Startup and stop commands
        var startup = root.TryGetProperty("startup", out var st) ? st.GetString() ?? "" : "";
        var stop = "";
        if (root.TryGetProperty("config", out var cfg) && cfg.TryGetProperty("stop", out var stopVal))
        {
            stop = stopVal.GetString() ?? "";
        }

        // Startup Done detection
        var startupDone = "";
        if (root.TryGetProperty("config", out var cfgDone) && cfgDone.TryGetProperty("startup", out var suObj))
        {
            if (suObj.TryGetProperty("done", out var doneVal))
            {
                startupDone = doneVal.GetString() ?? "";
            }
        }

        // Configuration files
        var configFiles = new List<ShardConfigFileDto>();
        if (root.TryGetProperty("config", out var cfgFiles) && cfgFiles.TryGetProperty("files", out var fObj) && fObj.ValueKind == JsonValueKind.Object)
        {
            foreach (var fileProp in fObj.EnumerateObject())
            {
                var filePath = fileProp.Name;
                var parser = "properties";
                var mappings = new Dictionary<string, string>();

                if (fileProp.Value.TryGetProperty("parser", out var pVal))
                    parser = pVal.GetString() ?? "properties";

                if (fileProp.Value.TryGetProperty("find", out var findObj) && findObj.ValueKind == JsonValueKind.Object)
                {
                    foreach (var m in findObj.EnumerateObject())
                    {
                        mappings[m.Name] = m.Value.GetString() ?? "";
                    }
                }

                configFiles.Add(new ShardConfigFileDto
                {
                    Path = filePath,
                    Parser = parser,
                    Mappings = mappings
                });
            }
        }

        // Installation scripts
        var installScript = "";
        var installImage = "ghcr.io/midnight-panel/installers:alpine";
        var installInterpreter = "/bin/bash";

        if (root.TryGetProperty("scripts", out var scripts) && scripts.TryGetProperty("installation", out var inst))
        {
            if (inst.TryGetProperty("script", out var sVal))
                installScript = sVal.GetString();

            if (inst.TryGetProperty("container", out var cVal))
                installImage = cVal.GetString() ?? installImage;

            if (inst.TryGetProperty("entrypoint", out var eVal))
                installInterpreter = eVal.GetString() ?? installInterpreter;
        }

        var now = _timeProvider.GetUtcNow();
        var shard = new Shard
        {
            Uuid = Guid.NewGuid(),
            CollectionId = collectionId,
            Name = uniqueName,
            Description = description,
            Author = author,
            Version = "1.0.0",
            DefaultDockerImage = defaultImage,
            StartupCommand = startup,
            StopCommand = stop,
            StopSignal = "SIGTERM",
            StartupDone = startupDone,
            InstallScript = installScript,
            InstallImage = installImage,
            InstallInterpreter = installInterpreter,
            CreatedAt = now,
            UpdatedAt = now
        };

        shard.SetDockerImages(images);
        shard.SetConfigFiles(configFiles);
        shard.SetFeatures(new ShardFeaturesDto());

        // Import variables, saved together with the shard
        if (root.TryGetProperty("variables", out var vars) && vars.ValueKind == JsonValueKind.Array)
        {
            var sort = 0;
            var envNames = new HashSet<string>();
            foreach (var v in vars.EnumerateArray())
            {
                var vName = v.TryGetProperty("name", out var vn) ? vn.GetString() ?? "Variable" : "Variable";
                var vDesc = v.TryGetProperty("description", out var vd) ? vd.GetString() : null;
                var vEnv = v.TryGetProperty("env_variable", out var ve) ? ve.GetString() ?? $"VAR_{sort}" : $"VAR_{sort}";
                var vDef = v.TryGetProperty("default_value", out var vdef) ? vdef.GetString() : "";
                var vView = !v.TryGetProperty("user_viewable", out var vv) || vv.GetBoolean();
                var vEdit = !v.TryGetProperty("user_editable", out var vee) || vee.GetBoolean();
                var vRules = v.TryGetProperty("rules", out var vr) ? vr.GetString() ?? "required|string" : "required|string";

                shard.Variables.Add(new ShardVariable
                {
                    Name = vName,
                    Description = vDesc,
                    EnvVariable = NormalizeEnvVariable(vEnv, envNames),
                    DefaultValue = vDef,
                    UserViewable = vView,
                    UserEditable = vEdit,
                    Rules = vRules,
                    SortOrder = ++sort
                });
            }
        }

        _context.Shards.Add(shard);
        await _context.SaveChangesAsync(cancellationToken);

        warnings.Add("Template was successfully converted from legacy format to modern Midnightpanel Shard format.");

        return new ShardImportResult
        {
            ShardId = shard.Id,
            ShardUuid = shard.Uuid,
            ShardName = shard.Name,
            CollectionId = collection.Id,
            CollectionName = collection.Name,
            DetectedFormat = "Legacy PTDL Format",
            Warnings = warnings
        };
    }

    /// <summary>Applies the same rules as <see cref="ShardVariableService"/> so imports can't create broken or duplicate variables</summary>
    private static string NormalizeEnvVariable(string? env, HashSet<string> seen)
    {
        var normalized = env?.Trim().ToUpperInvariant() ?? "";

        if (!EnvVariableRegex().IsMatch(normalized) || StartupCommandResolver.SystemVariables.Contains(normalized))
            throw HttpApiException.BadRequest("Invalid variable", $"'{env}' is not a valid or allowed environment variable name.");

        if (!seen.Add(normalized))
            throw HttpApiException.BadRequest("Invalid variable", $"The variable '{normalized}' is defined more than once.");

        return normalized;
    }

    private async Task<int> EnsureCollectionAsync(string name, string? description, string author, CancellationToken cancellationToken)
    {
        var existing = await _context.ShardCollections.FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower(), cancellationToken);
        if (existing != null)
            return existing.Id;

        var now = _timeProvider.GetUtcNow();
        var collection = new ShardCollection
        {
            Uuid = Guid.NewGuid(),
            Name = name,
            Description = description,
            Author = string.IsNullOrWhiteSpace(author) ? "Midnightpanel" : author,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ShardCollections.Add(collection);
        await _context.SaveChangesAsync(cancellationToken);
        return collection.Id;
    }

    private async Task<string> GetUniqueShardNameAsync(int collectionId, string baseName, CancellationToken cancellationToken)
    {
        var name = baseName;
        var counter = 1;
        while (await _context.Shards.AnyAsync(x => x.CollectionId == collectionId && x.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            name = $"{baseName} ({++counter})";
        }
        return name;
    }

    public const int MaxBatchItems = 200;
    public const long MaxShardFileSize = 2L * 1024 * 1024;
    public const long MaxUploadSize = 50L * 1024 * 1024;
    private const long MaxArchiveExtractedSize = 100L * 1024 * 1024;
    private const int MaxArchiveEntries = 1000;

    public async Task<ShardBatchImportResult> ImportBatchAsync(ShardBatchImportRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count > MaxBatchItems)
            throw HttpApiException.BadRequest("Too many shards", $"A batch import may contain at most {MaxBatchItems} shards.");

        var results = new List<ShardBatchImportItemResult>();

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Content))
                continue;

            try
            {
                var importRes = await ImportAsync(new ShardImportRequest
                {
                    TargetCollectionId = request.TargetCollectionId,
                    Content = item.Content
                }, cancellationToken);

                results.Add(new ShardBatchImportItemResult
                {
                    FileName = item.FileName ?? importRes.ShardName,
                    Success = true,
                    Shard = importRes
                });
            }
            catch (Exception ex)
            {
                results.Add(new ShardBatchImportItemResult
                {
                    FileName = item.FileName ?? "Unknown",
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return new ShardBatchImportResult
        {
            TotalCount = results.Count,
            SucceededCount = results.Count(x => x.Success),
            FailedCount = results.Count(x => !x.Success),
            Results = results
        };
    }

    public async Task<ShardBatchImportResult> ImportFilesAsync(IFormFileCollection files, int? targetCollectionId, CancellationToken cancellationToken)
    {
        var batchItems = new List<ShardBatchImportItem>();
        long extractedTotal = 0;

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            if (file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await using var stream = file.OpenReadStream();
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                    SafeZip.EnsureEntryCount(archive, MaxArchiveEntries);
                    foreach (var entry in archive.Entries)
                    {
                        // Entries are only read into memory, never written to disk; still skip odd paths
                        if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("__MACOSX", StringComparison.Ordinal) ||
                            entry.FullName.Contains("..", StringComparison.Ordinal))
                            continue;

                        extractedTotal += Math.Min(entry.Length, MaxShardFileSize);
                        if (extractedTotal > MaxArchiveExtractedSize)
                            throw new InvalidDataException($"The archives exceed the maximum extracted size of {MaxArchiveExtractedSize / (1024 * 1024)} MB.");

                        var jsonText = await SafeZip.ReadAllTextAsync(entry, MaxShardFileSize, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(jsonText))
                        {
                            AddBatchItem(batchItems, entry.Name, jsonText);
                        }
                    }
                }
                catch (InvalidDataException ex)
                {
                    throw HttpApiException.BadRequest("Invalid archive", $"{Path.GetFileName(file.FileName)}: {ex.Message}");
                }
            }
            else if (file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (file.Length > MaxShardFileSize)
                    throw HttpApiException.BadRequest("File too large",
                        $"{Path.GetFileName(file.FileName)} exceeds the maximum size of {MaxShardFileSize / 1024} KB.");

                using var reader = new StreamReader(file.OpenReadStream());
                var jsonText = await reader.ReadToEndAsync(cancellationToken);
                AddBatchItem(batchItems, Path.GetFileName(file.FileName), jsonText);
            }
        }

        return await ImportBatchAsync(new ShardBatchImportRequest
        {
            TargetCollectionId = targetCollectionId,
            Items = batchItems
        }, cancellationToken);
    }

    private static void AddBatchItem(List<ShardBatchImportItem> items, string fileName, string content)
    {
        if (items.Count >= MaxBatchItems)
            throw HttpApiException.BadRequest("Too many shards", $"A batch import may contain at most {MaxBatchItems} shards.");
        items.Add(new ShardBatchImportItem { FileName = fileName, Content = content });
    }
}
