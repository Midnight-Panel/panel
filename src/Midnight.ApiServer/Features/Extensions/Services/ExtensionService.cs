using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Extensions.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Extensions;

namespace Midnight.ApiServer.Features.Extensions.Services;

public partial class ExtensionService : IExtensionService
{
    public const long MaxPackageSize = 50L * 1024 * 1024;
    private const long MaxExtractedSize = 200L * 1024 * 1024;
    private const int MaxArchiveEntries = 2000;
    private const long MaxManifestSize = 1024 * 1024;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Identifiers are used as file and directory names, so only a safe charset is allowed</summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}$")]
    private static partial Regex SafeIdentifierRegex();

    public static bool IsSafeIdentifier(string? identifier) =>
        !string.IsNullOrEmpty(identifier) && SafeIdentifierRegex().IsMatch(identifier);

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
        if (!IsSafeIdentifier(identifier))
            throw HttpApiException.BadRequest("Invalid identifier", "Use 1-64 lowercase letters, digits, '.', '-' or '_', starting with a letter or digit.");

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
        FileStream package;
        try
        {
            package = await SafeDownloader.CopyToTempFileAsync(fileStream, MaxPackageSize, cancellationToken);
        }
        catch (SafeDownloadException ex)
        {
            throw HttpApiException.BadRequest("Package too large", ex.Message);
        }

        await using var _ = package;

        return await ProcessAndSavePackageAsync(package, Path.GetFileName(fileName), installSource: "file", sourceUrl: null, cancellationToken);
    }

    public async Task<ExtensionDetailResponse> InstallFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!SafeDownloader.TryParseHttpUrl(url, out var uri))
            throw HttpApiException.BadRequest("Please provide a valid http:// or https:// URL.");

        SafeDownloadResult download;
        try
        {
            download = await SafeDownloader.DownloadToTempFileAsync(
                uri.AbsoluteUri, MaxPackageSize, DownloadTimeout, "MidnightPanel-ExtensionInstaller/1.0", cancellationToken);
        }
        catch (SafeDownloadException ex)
        {
            _logger.LogWarning(ex, "Failed to download extension package from {Url}", uri);
            throw HttpApiException.BadRequest("Download failed", ex.Message);
        }

        await using (download)
        {
            var fileName = Path.GetFileName(download.FinalUri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
            {
                fileName = "extension.zip";
            }

            return await ProcessAndSavePackageAsync(download.Content, fileName, installSource: "url", sourceUrl: uri.AbsoluteUri, cancellationToken);
        }
    }

    public async Task<(Stream fileStream, string fileName, string contentType)> DownloadPackageAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound("Extension not found.");
        if (!IsSafeIdentifier(entity.Identifier))
            throw HttpApiException.NotFound("No package file found for this extension.");

        var packagePath = Path.Combine(_storagePaths.ExtensionPackages, $"{entity.Identifier}.zip");
        if (File.Exists(packagePath))
        {
            var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (stream, entity.PackageFileName ?? $"{entity.Identifier}-v{entity.Version}.zip", "application/zip");
        }

        // No stored package but an installed directory: zip it on the fly
        var installedDir = Path.Combine(_storagePaths.ExtensionInstalled, entity.Identifier);
        if (Directory.Exists(installedDir))
        {
            var ms = new MemoryStream();
            ZipFile.CreateFromDirectory(installedDir, ms);
            ms.Position = 0;
            return (ms, $"{entity.Identifier}-v{entity.Version}.zip", "application/zip");
        }

        throw HttpApiException.NotFound("No package file found for this extension.");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound("Extension not found.");

        _context.Extensions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        if (!IsSafeIdentifier(entity.Identifier))
        {
            _logger.LogInformation("Deleted extension {Identifier}", entity.Identifier);
            return;
        }

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
        FileStream stream,
        string fileName,
        string installSource,
        string? sourceUrl,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_storagePaths.ExtensionPackages);
        Directory.CreateDirectory(_storagePaths.ExtensionInstalled);

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "extension.zip";
        if (fileName.Length > 255)
            fileName = fileName[^255..];

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            throw HttpApiException.BadRequest("Invalid package", "The file is not a valid ZIP archive.");
        }

        using var _ = archive;
        string identifier;
        string installDir;
        ExtensionInstallRequest manifest;
        try
        {
            SafeZip.EnsureEntryCount(archive, MaxArchiveEntries);
            manifest = await ReadManifestAsync(archive, fileName, cancellationToken);
            identifier = manifest.Identifier.Trim().ToLowerInvariant();
            if (!IsSafeIdentifier(identifier))
                throw HttpApiException.BadRequest("Invalid identifier",
                    $"The extension identifier '{manifest.Identifier}' may only contain lowercase letters, digits, '.', '-' and '_' (max. 64 characters).");

            // 1. Extract into a temporary directory first so a broken package never replaces a working install
            installDir = Path.Combine(_storagePaths.ExtensionInstalled, identifier);
            var tempDir = Path.Combine(_storagePaths.ExtensionInstalled, $".tmp-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            try
            {
                await SafeZip.ExtractToDirectoryAsync(archive, tempDir, MaxExtractedSize, MaxArchiveEntries, cancellationToken);
            }
            catch
            {
                Directory.Delete(tempDir, recursive: true);
                throw;
            }

            if (Directory.Exists(installDir))
                Directory.Delete(installDir, recursive: true);
            Directory.Move(tempDir, installDir);
        }
        catch (InvalidDataException ex)
        {
            throw HttpApiException.BadRequest("Invalid package", ex.Message);
        }

        // 2. Store the ZIP package
        var packagePath = Path.Combine(_storagePaths.ExtensionPackages, $"{identifier}.zip");
        stream.Position = 0;
        await using (var fileOut = new FileStream(packagePath, FileMode.Create, FileAccess.Write))
        {
            await stream.CopyToAsync(fileOut, cancellationToken);
        }

        // 3. Create or update the database entry
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
            ReadmeMarkdown = manifest.ReadmeMarkdown ?? $"# {manifest.Name}\n\nInstalled from package `{fileName}`.",
            InstalledAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Extensions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDetail(entity, GetInstalledFiles(identifier));
    }

    private async Task<ExtensionInstallRequest> ReadManifestAsync(ZipArchive archive, string fileName, CancellationToken cancellationToken)
    {
        var manifestEntry = archive.GetEntry("extension.json") ?? archive.GetEntry("manifest.json");

        ExtensionInstallRequest? manifest = null;
        if (manifestEntry != null)
        {
            try
            {
                var json = await SafeZip.ReadAllTextAsync(manifestEntry, MaxManifestSize, cancellationToken);
                manifest = JsonSerializer.Deserialize<ExtensionInstallRequest>(json, JsonDefaults.Options);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse extension.json in archive {FileName}", fileName);
            }
        }

        // No manifest found: build a valid default manifest from the file name
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Identifier))
        {
            var rawName = Path.GetFileNameWithoutExtension(fileName);
            var sanitizedIdentifier = Regex.Replace(rawName.ToLowerInvariant(), @"[^a-z0-9-_]", "-").Trim('-', '_');
            if (sanitizedIdentifier.Length > 64)
                sanitizedIdentifier = sanitizedIdentifier[..64].TrimEnd('-', '_');
            if (string.IsNullOrWhiteSpace(sanitizedIdentifier))
                sanitizedIdentifier = "extension-" + Guid.NewGuid().ToString("N")[..8];

            manifest = new ExtensionInstallRequest
            {
                Identifier = sanitizedIdentifier,
                Name = rawName.Replace('-', ' ').Replace('_', ' '),
                Description = $"Installed from package '{fileName}'.",
                Version = "1.0.0",
                Author = "Community",
                Category = "Tools",
                Icon = "plugin"
            };
        }

        var readmeEntry = archive.GetEntry("README.md") ?? archive.GetEntry("readme.md") ?? archive.GetEntry("README.txt");
        if (readmeEntry != null)
        {
            manifest = manifest with { ReadmeMarkdown = await SafeZip.ReadAllTextAsync(readmeEntry, MaxManifestSize, cancellationToken) };
        }

        return manifest;
    }

    private List<string> GetInstalledFiles(string identifier)
    {
        if (!IsSafeIdentifier(identifier))
            return [];

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
                Description = "Sends automatic event and status notifications to configured Discord webhooks.",
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
                    "notifyOnServerStart": { "type": "boolean", "title": "Notify on server start", "default": true },
                    "notifyOnServerCrash": { "type": "boolean", "title": "Notify on server crash", "default": true }
                  }
                }
                """,
                ConfigValuesJson = """{"webhookUrl": "", "notifyOnServerStart": true, "notifyOnServerCrash": true}""",
                ReadmeMarkdown = "# Discord Webhook Notifier\n\nConnect your game servers to Discord.",
                InstalledAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Identifier = "backup-scheduler",
                Name = "Automated Backup Scheduler",
                Description = "Advanced backup schedules with cron syntax, retention policies and S3 off-site upload.",
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
                    "cronExpression": { "type": "string", "title": "Default cron expression", "default": "0 4 * * *" },
                    "retentionDays": { "type": "number", "title": "Retention days", "default": 7 },
                    "compressLevel": { "type": "string", "title": "Compression level", "enum": ["Fast", "Optimal", "Maximum"], "default": "Optimal" }
                  }
                }
                """,
                ConfigValuesJson = """{"cronExpression": "0 4 * * *", "retentionDays": 7, "compressLevel": "Optimal"}""",
                ReadmeMarkdown = "# Automated Backup Scheduler\n\nAdvanced backup management for Midnightpanel.",
                InstalledAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Identifier = "cloudflare-dns",
                Name = "Cloudflare DNS Manager",
                Description = "Automatically creates subdomains (SRV/A records) for newly created game servers.",
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
                    "baseDomain": { "type": "string", "title": "Base domain", "default": "arwenagg.de" }
                  }
                }
                """,
                ConfigValuesJson = """{"apiToken": "", "zoneId": "", "baseDomain": "arwenagg.de"}""",
                ReadmeMarkdown = "# Cloudflare Subdomain Automator\n\nAutomatic DNS records for your servers.",
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
