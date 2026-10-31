using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Extensions.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Extensions;

namespace Midnight.ApiServer.Features.Extensions.Services;

public class ExtensionService : IExtensionService
{
    private readonly DataContext _context;
    private readonly StoragePaths _storagePaths;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExtensionService> _logger;

    public ExtensionService(
        DataContext context,
        StoragePaths storagePaths,
        IHttpClientFactory httpClientFactory,
        ILogger<ExtensionService> logger)
    {
        _context = context;
        _storagePaths = storagePaths;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PagedResult<ExtensionSummaryResponse>> ListAsync(PagedQuery query, string? category = null, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);

        var extensions = _context.Extensions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            extensions = extensions.Where(x => x.Category.ToLower() == category.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            extensions = extensions.Where(x =>
                x.Name.ToLower().Contains(search) ||
                x.Identifier.ToLower().Contains(search) ||
                x.Description.ToLower().Contains(search) ||
                x.Author.ToLower().Contains(search));
        }

        extensions = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", false) => extensions.OrderBy(x => x.Name),
            ("name", true) => extensions.OrderByDescending(x => x.Name),
            ("category", false) => extensions.OrderBy(x => x.Category),
            ("category", true) => extensions.OrderByDescending(x => x.Category),
            ("version", false) => extensions.OrderBy(x => x.Version),
            ("version", true) => extensions.OrderByDescending(x => x.Version),
            (_, true) => extensions.OrderByDescending(x => x.Id),
            _ => extensions.OrderBy(x => x.Id)
        };

        var total = await extensions.CountAsync(cancellationToken);

        var items = await extensions
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(x => MapToSummary(x))
            .ToListAsync(cancellationToken);

        return new PagedResult<ExtensionSummaryResponse>
        {
            Items = items,
            TotalCount = total,
            Page = query.SafePage,
            PageSize = query.SafePageSize
        };
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        return await _context.Extensions
            .AsNoTracking()
            .Select(x => x.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExtensionDetailResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound("Extension not found.");

        return MapToDetail(entity, GetInstalledFiles(entity.Identifier));
    }

    public async Task<ExtensionDetailResponse> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.AsNoTracking().FirstOrDefaultAsync(x => x.Identifier == identifier, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound($"Extension '{identifier}' not found.");

        return MapToDetail(entity, GetInstalledFiles(entity.Identifier));
    }

    public async Task<ExtensionDetailResponse> ToggleAsync(int id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound("Extension not found.");

        entity.IsEnabled = isEnabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Toggled extension {Identifier} enabled to {IsEnabled}", entity.Identifier, isEnabled);
        return MapToDetail(entity, GetInstalledFiles(entity.Identifier));
    }

    public async Task<ExtensionDetailResponse> UpdateConfigAsync(int id, string configValuesJson, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound("Extension not found.");

        try
        {
            using var doc = JsonDocument.Parse(configValuesJson);
        }
        catch (Exception ex)
        {
            throw HttpApiException.BadRequest($"Invalid JSON configuration format: {ex.Message}");
        }

        entity.ConfigValuesJson = configValuesJson;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated configuration for extension {Identifier}", entity.Identifier);
        return MapToDetail(entity, GetInstalledFiles(entity.Identifier));
    }

    public async Task<ExtensionDetailResponse> InstallAsync(ExtensionInstallRequest request, CancellationToken cancellationToken = default)
    {
        var identifier = request.Identifier.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(identifier))
            throw HttpApiException.BadRequest("Identifier must not be empty.");

        var existing = await _context.Extensions.FirstOrDefaultAsync(x => x.Identifier == identifier, cancellationToken);
        if (existing != null)
            throw HttpApiException.BadRequest($"An extension with the identifier '{identifier}' is already installed.");

        var entity = new ExtensionEntity
        {
            Identifier = identifier,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Version = string.IsNullOrWhiteSpace(request.Version) ? "1.0.0" : request.Version.Trim(),
            Author = string.IsNullOrWhiteSpace(request.Author) ? "Midnightpanel" : request.Author.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Tools" : request.Category.Trim(),
            WebsiteUrl = request.WebsiteUrl,
            Icon = request.Icon,
            InstallSource = string.IsNullOrWhiteSpace(request.InstallSource) ? "file" : request.InstallSource,
            SourceUrl = request.SourceUrl,
            PackageFileName = request.PackageFileName,
            IsEnabled = true,
            ConfigSchemaJson = request.ConfigSchemaJson,
            ConfigValuesJson = request.ConfigValuesJson ?? "{}",
            ReadmeMarkdown = request.ReadmeMarkdown,
            InstalledAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Extensions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDetail(entity, GetInstalledFiles(entity.Identifier));
    }

    public async Task<ExtensionDetailResponse> UploadPackageAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        return await ProcessAndSavePackageAsync(memoryStream, fileName, installSource: "file", sourceUrl: null, cancellationToken);
    }

    public async Task<ExtensionDetailResponse> InstallFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            throw HttpApiException.BadRequest("Bitte gib eine gültige HTTP- oder HTTPS-URL an.");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MidnightPanel-ExtensionInstaller/1.0");

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download extension package from {Url}", url);
            throw HttpApiException.BadRequest($"Download fehlgeschlagen: {ex.Message}");
        }

        var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
        {
            fileName = "extension.zip";
        }

        await using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memoryStream = new MemoryStream();
        await downloadStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        return await ProcessAndSavePackageAsync(memoryStream, fileName, installSource: "url", sourceUrl: url, cancellationToken);
    }

    public async Task<(Stream fileStream, string fileName, string contentType)> DownloadPackageAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound("Erweiterung wurde nicht gefunden.");

        var packagePath = Path.Combine(_storagePaths.ExtensionPackages, $"{entity.Identifier}.zip");
        if (File.Exists(packagePath))
        {
            var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (stream, entity.PackageFileName ?? $"{entity.Identifier}-v{entity.Version}.zip", "application/zip");
        }

        // Falls Paketdatei nicht existiert, aber Ordner installiert ist: on-the-fly zippen
        var installedDir = Path.Combine(_storagePaths.ExtensionInstalled, entity.Identifier);
        if (Directory.Exists(installedDir))
        {
            var ms = new MemoryStream();
            ZipFile.CreateFromDirectory(installedDir, ms);
            ms.Position = 0;
            return (ms, $"{entity.Identifier}-v{entity.Version}.zip", "application/zip");
        }

        throw HttpApiException.NotFound("Keine Paketdatei für diese Erweiterung gefunden.");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound("Extension not found.");

        _context.Extensions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var pkg = Path.Combine(_storagePaths.ExtensionPackages, $"{entity.Identifier}.zip");
            if (File.Exists(pkg)) File.Delete(pkg);

            var dir = Path.Combine(_storagePaths.ExtensionInstalled, entity.Identifier);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup files for deleted extension {Identifier}", entity.Identifier);
        }

        _logger.LogInformation("Deleted extension {Identifier}", entity.Identifier);
    }

    private async Task<ExtensionDetailResponse> ProcessAndSavePackageAsync(
        MemoryStream stream,
        string fileName,
        string installSource,
        string? sourceUrl,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_storagePaths.ExtensionPackages);
        Directory.CreateDirectory(_storagePaths.ExtensionInstalled);

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry("extension.json") ?? archive.GetEntry("manifest.json");

        ExtensionInstallRequest? manifest = null;
        if (manifestEntry != null)
        {
            try
            {
                using var mStream = manifestEntry.Open();
                manifest = await JsonSerializer.DeserializeAsync<ExtensionInstallRequest>(mStream, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse extension.json in archive {FileName}", fileName);
            }
        }

        // Falls kein Manifest gefunden wurde, erzeuge ein valides Standard-Manifest aus dem Dateinamen
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Identifier))
        {
            var rawName = Path.GetFileNameWithoutExtension(fileName);
            var sanitizedIdentifier = Regex.Replace(rawName.ToLowerInvariant(), @"[^a-z0-9-_]", "-").Trim('-');
            if (string.IsNullOrWhiteSpace(sanitizedIdentifier))
                sanitizedIdentifier = "extension-" + Guid.NewGuid().ToString("N")[..8];

            manifest = new ExtensionInstallRequest
            {
                Identifier = sanitizedIdentifier,
                Name = rawName.Replace('-', ' ').Replace('_', ' '),
                Description = $"Aus Paket '{fileName}' installiert.",
                Version = "1.0.0",
                Author = "Community",
                Category = "Tools",
                Icon = "plugin"
            };
        }

        var readmeEntry = archive.GetEntry("README.md") ?? archive.GetEntry("readme.md") ?? archive.GetEntry("README.txt");
        if (readmeEntry != null)
        {
            using var reader = new StreamReader(readmeEntry.Open());
            manifest = manifest with { ReadmeMarkdown = await reader.ReadToEndAsync(cancellationToken) };
        }

        var identifier = manifest.Identifier.Trim().ToLowerInvariant();

        // 1. Speichere das ZIP-Paket
        var packagePath = Path.Combine(_storagePaths.ExtensionPackages, $"{identifier}.zip");
        stream.Position = 0;
        await using (var fileOut = new FileStream(packagePath, FileMode.Create, FileAccess.Write))
        {
            await stream.CopyToAsync(fileOut, cancellationToken);
        }

        // 2. Entpacke die Dateien
        var installDir = Path.Combine(_storagePaths.ExtensionInstalled, identifier);
        if (Directory.Exists(installDir))
        {
            Directory.Delete(installDir, recursive: true);
        }
        Directory.CreateDirectory(installDir);
        archive.ExtractToDirectory(installDir, overwriteFiles: true);

        // 3. Datenbank-Eintrag anlegen oder aktualisieren
        var existing = await _context.Extensions.FirstOrDefaultAsync(x => x.Identifier == identifier, cancellationToken);
        if (existing != null)
        {
            existing.Name = manifest.Name;
            existing.Description = manifest.Description;
            existing.Version = manifest.Version;
            existing.Author = manifest.Author;
            existing.Category = manifest.Category;
            existing.WebsiteUrl = manifest.WebsiteUrl ?? existing.WebsiteUrl;
            existing.Icon = manifest.Icon ?? existing.Icon;
            existing.InstallSource = installSource;
            existing.SourceUrl = sourceUrl;
            existing.PackageFileName = fileName;
            existing.StoragePath = installDir;
            if (!string.IsNullOrWhiteSpace(manifest.ReadmeMarkdown))
                existing.ReadmeMarkdown = manifest.ReadmeMarkdown;
            if (!string.IsNullOrWhiteSpace(manifest.ConfigSchemaJson))
                existing.ConfigSchemaJson = manifest.ConfigSchemaJson;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return MapToDetail(existing, GetInstalledFiles(identifier));
        }

        var entity = new ExtensionEntity
        {
            Identifier = identifier,
            Name = manifest.Name,
            Description = manifest.Description,
            Version = manifest.Version,
            Author = manifest.Author,
            Category = manifest.Category,
            WebsiteUrl = manifest.WebsiteUrl,
            Icon = manifest.Icon,
            InstallSource = installSource,
            SourceUrl = sourceUrl,
            PackageFileName = fileName,
            StoragePath = installDir,
            IsEnabled = true,
            ConfigSchemaJson = manifest.ConfigSchemaJson,
            ConfigValuesJson = manifest.ConfigValuesJson ?? "{}",
            ReadmeMarkdown = manifest.ReadmeMarkdown ?? $"# {manifest.Name}\n\nInstalliert aus Paket `{fileName}`.",
            InstalledAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Extensions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDetail(entity, GetInstalledFiles(identifier));
    }

    private List<string> GetInstalledFiles(string identifier)
    {
        try
        {
            var dir = Path.Combine(_storagePaths.ExtensionInstalled, identifier);
            if (!Directory.Exists(dir)) return [];
            return Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'))
                .Take(100)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        if (await _context.Extensions.AnyAsync(cancellationToken))
            return;

        var defaults = new List<ExtensionEntity>
        {
            new()
            {
                Identifier = "discord-webhook",
                Name = "Discord Webhook Notifier",
                Description = "Sendet automatische Ereignis- und Status-Mitteilungen an konfigurierte Discord-Webhooks.",
                Category = "Integration",
                Version = "1.2.0",
                Author = "Midnightpanel Team",
                Icon = "chat",
                InstallSource = "builtin",
                WebsiteUrl = "https://midnightpanel.com",
                IsEnabled = true,
                ConfigSchemaJson = """
                {
                  "type": "object",
                  "properties": {
                    "webhookUrl": { "type": "string", "title": "Discord Webhook URL", "format": "uri" },
                    "notifyOnServerStart": { "type": "boolean", "title": "Bei Server-Start benachrichtigen", "default": true },
                    "notifyOnServerCrash": { "type": "boolean", "title": "Bei Server-Crash benachrichtigen", "default": true }
                  }
                }
                """,
                ConfigValuesJson = """{"webhookUrl": "", "notifyOnServerStart": true, "notifyOnServerCrash": true}""",
                ReadmeMarkdown = "# Discord Webhook Notifier\n\nVerbinde deine Spieleserver mit Discord.",
                InstalledAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Identifier = "backup-scheduler",
                Name = "Automated Backup Scheduler",
                Description = "Erweiterte Backup-Pläne mit Cron-Syntax, Aufbewahrungsrichtlinien und S3-Offsite-Upload.",
                Category = "Backups",
                Version = "2.0.1",
                Author = "Midnightpanel Team",
                Icon = "database",
                InstallSource = "builtin",
                WebsiteUrl = "https://midnightpanel.com",
                IsEnabled = true,
                ConfigSchemaJson = """
                {
                  "type": "object",
                  "properties": {
                    "cronExpression": { "type": "string", "title": "Standard-Cron-Ausdruck", "default": "0 4 * * *" },
                    "retentionDays": { "type": "number", "title": "Aufbewahrungstage", "default": 7 },
                    "compressLevel": { "type": "string", "title": "Kompressionsstufe", "enum": ["Fast", "Optimal", "Maximum"], "default": "Optimal" }
                  }
                }
                """,
                ConfigValuesJson = """{"cronExpression": "0 4 * * *", "retentionDays": 7, "compressLevel": "Optimal"}""",
                ReadmeMarkdown = "# Automated Backup Scheduler\n\nErweiterte Backup-Verwaltung für Midnightpanel.",
                InstalledAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Identifier = "cloudflare-dns",
                Name = "Cloudflare DNS Manager",
                Description = "Automatisches Anlegen von Subdomains (SRV/A-Records) für neu erstellte Gameserver.",
                Category = "Tools",
                Version = "1.0.4",
                Author = "Midnightpanel Team",
                Icon = "globe",
                InstallSource = "builtin",
                WebsiteUrl = "https://midnightpanel.com",
                IsEnabled = false,
                ConfigSchemaJson = """
                {
                  "type": "object",
                  "properties": {
                    "apiToken": { "type": "string", "title": "Cloudflare API Token", "format": "password" },
                    "zoneId": { "type": "string", "title": "Cloudflare Zone ID" },
                    "baseDomain": { "type": "string", "title": "Basis-Domain", "default": "arwenagg.de" }
                  }
                }
                """,
                ConfigValuesJson = """{"apiToken": "", "zoneId": "", "baseDomain": "arwenagg.de"}""",
                ReadmeMarkdown = "# Cloudflare Subdomain Automator\n\nAutomatische DNS-Einträge für deine Server.",
                InstalledAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        _context.Extensions.AddRange(defaults);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static ExtensionSummaryResponse MapToSummary(ExtensionEntity entity) => new()
    {
        Id = entity.Id,
        Identifier = entity.Identifier,
        Name = entity.Name,
        Description = entity.Description,
        Version = entity.Version,
        Author = entity.Author,
        Category = entity.Category,
        Icon = entity.Icon,
        WebsiteUrl = entity.WebsiteUrl,
        InstallSource = entity.InstallSource,
        SourceUrl = entity.SourceUrl,
        PackageFileName = entity.PackageFileName,
        IsEnabled = entity.IsEnabled,
        InstalledAt = entity.InstalledAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static ExtensionDetailResponse MapToDetail(ExtensionEntity entity, List<string>? installedFiles = null) => new()
    {
        Id = entity.Id,
        Identifier = entity.Identifier,
        Name = entity.Name,
        Description = entity.Description,
        Version = entity.Version,
        Author = entity.Author,
        Category = entity.Category,
        Icon = entity.Icon,
        WebsiteUrl = entity.WebsiteUrl,
        InstallSource = entity.InstallSource,
        SourceUrl = entity.SourceUrl,
        PackageFileName = entity.PackageFileName,
        IsEnabled = entity.IsEnabled,
        ConfigSchemaJson = entity.ConfigSchemaJson,
        ConfigValuesJson = entity.ConfigValuesJson,
        ReadmeMarkdown = entity.ReadmeMarkdown,
        InstalledFiles = installedFiles ?? [],
        RequiredPermissions = [],
        InstalledAt = entity.InstalledAt,
        UpdatedAt = entity.UpdatedAt
    };
}
