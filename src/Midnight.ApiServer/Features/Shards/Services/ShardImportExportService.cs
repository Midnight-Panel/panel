using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Services;

public class ShardImportExportService : IShardImportExportService
{
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
            ?? throw HttpApiException.NotFound("Shard nicht gefunden");

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
            throw HttpApiException.BadRequest("Der Import-Inhalt ist leer.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(request.Content);
        }
        catch (JsonException ex)
        {
            throw HttpApiException.BadRequest("Ungültiges JSON-Format", ex.Message);
        }

        using (doc)
        {
            var root = doc.RootElement;

            // Erkennung des Formats
            if (root.TryGetProperty("formatVersion", out var fvProp) && fvProp.GetString() == ShardExportPackage.CurrentFormatVersion)
            {
                return await ImportMidnightFormatAsync(root, request.TargetCollectionId, cancellationToken);
            }

            // Erkennung des PTDL Legacy-Formats (PTDL_v1 / PTDL_v2)
            if (root.TryGetProperty("meta", out var metaProp) || root.TryGetProperty("_comment", out _) || root.TryGetProperty("exported_at", out _))
            {
                return await ImportPtdlLegacyFormatAsync(root, request.TargetCollectionId, cancellationToken);
            }

            throw HttpApiException.BadRequest("Unbekanntes Dateiformat", "Die JSON-Datei konnte weder als Midnight-Shard noch als Legacy-Format identifiziert werden.");
        }
    }

    private async Task<ShardImportResult> ImportMidnightFormatAsync(JsonElement root, int? targetCollectionId, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var package = root.Deserialize<ShardExportPackage>(options)
                      ?? throw HttpApiException.BadRequest("Fehler beim Deserialisieren des Shard-Pakets.");

        var collectionId = targetCollectionId ?? await EnsureCollectionAsync(package.Collection.Name, package.Collection.Description, package.Collection.Author, cancellationToken);
        var collection = await _context.ShardCollections.FirstAsync(x => x.Id == collectionId, cancellationToken);

        // Name anpassen, falls bereits vorhanden
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

        _context.Shards.Add(shard);
        await _context.SaveChangesAsync(cancellationToken);

        // Variablen hinzufügen
        var sort = 0;
        foreach (var v in package.Shard.Variables)
        {
            _context.ShardVariables.Add(new ShardVariable
            {
                ShardId = shard.Id,
                Name = v.Name,
                Description = v.Description,
                EnvVariable = v.EnvVariable.ToUpperInvariant(),
                DefaultValue = v.DefaultValue,
                UserViewable = v.UserViewable,
                UserEditable = v.UserEditable,
                Rules = string.IsNullOrWhiteSpace(v.Rules) ? "required|string" : v.Rules,
                SortOrder = v.SortOrder > 0 ? v.SortOrder : ++sort
            });
        }

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

        var collectionId = targetCollectionId ?? await EnsureCollectionAsync("Importiert", "Sammlung für importierte Vorlagen", "Midnightpanel", cancellationToken);
        var collection = await _context.ShardCollections.FirstAsync(x => x.Id == collectionId, cancellationToken);
        var uniqueName = await GetUniqueShardNameAsync(collectionId, name, cancellationToken);

        // Docker Images auslesen
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
                images["Standard"] = imgStr;
                defaultImage = imgStr;
            }
        }

        if (string.IsNullOrEmpty(defaultImage) && images.Count > 0)
        {
            defaultImage = images.Values.First();
        }

        // Start- und Stopp-Befehle
        var startup = root.TryGetProperty("startup", out var st) ? st.GetString() ?? "" : "";
        var stop = "";
        if (root.TryGetProperty("config", out var cfg) && cfg.TryGetProperty("stop", out var stopVal))
        {
            stop = stopVal.GetString() ?? "";
        }

        // Startup Done
        var startupDone = "";
        if (root.TryGetProperty("config", out var cfgDone) && cfgDone.TryGetProperty("startup", out var suObj))
        {
            if (suObj.TryGetProperty("done", out var doneVal))
            {
                startupDone = doneVal.GetString() ?? "";
            }
        }

        // Konfigurationsdateien
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

        // Installationsskripte
        var installScript = "";
        var installImage = "ghcr.io/midnightpanel/installers:alpine";
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

        _context.Shards.Add(shard);
        await _context.SaveChangesAsync(cancellationToken);

        // Variablen importieren
        if (root.TryGetProperty("variables", out var vars) && vars.ValueKind == JsonValueKind.Array)
        {
            var sort = 0;
            foreach (var v in vars.EnumerateArray())
            {
                var vName = v.TryGetProperty("name", out var vn) ? vn.GetString() ?? "Variable" : "Variable";
                var vDesc = v.TryGetProperty("description", out var vd) ? vd.GetString() : null;
                var vEnv = v.TryGetProperty("env_variable", out var ve) ? ve.GetString() ?? $"VAR_{sort}" : $"VAR_{sort}";
                var vDef = v.TryGetProperty("default_value", out var vdef) ? vdef.GetString() : "";
                var vView = !v.TryGetProperty("user_viewable", out var vv) || vv.GetBoolean();
                var vEdit = !v.TryGetProperty("user_editable", out var vee) || vee.GetBoolean();
                var vRules = v.TryGetProperty("rules", out var vr) ? vr.GetString() ?? "required|string" : "required|string";

                _context.ShardVariables.Add(new ShardVariable
                {
                    ShardId = shard.Id,
                    Name = vName,
                    Description = vDesc,
                    EnvVariable = vEnv.ToUpperInvariant(),
                    DefaultValue = vDef,
                    UserViewable = vView,
                    UserEditable = vEdit,
                    Rules = vRules,
                    SortOrder = ++sort
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        warnings.Add("Die Vorlage wurde erfolgreich aus einem Legacy-Format in das moderne Midnightpanel Shard-Format konvertiert.");

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
}
