using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Extensions.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Extensions;

namespace Midnight.ApiServer.Features.Extensions.Services;

public class ExtensionService : IExtensionService
{
    private readonly DataContext _context;
    private readonly ILogger<ExtensionService> _logger;

    public ExtensionService(DataContext context, ILogger<ExtensionService> logger)
    {
        _context = context;
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

        return MapToDetail(entity);
    }

    public async Task<ExtensionDetailResponse> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.AsNoTracking().FirstOrDefaultAsync(x => x.Identifier == identifier, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound($"Extension '{identifier}' not found.");

        return MapToDetail(entity);
    }

    public async Task<ExtensionDetailResponse> ToggleAsync(int id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound("Extension not found.");

        entity.IsEnabled = isEnabled;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Extension {Identifier} is now {State}", entity.Identifier, isEnabled ? "enabled" : "disabled");
        return MapToDetail(entity);
    }

    public async Task<ExtensionDetailResponse> UpdateConfigAsync(int id, string configValuesJson, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound("Extension not found.");

        try
        {
            // Validate that it is valid JSON
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
        return MapToDetail(entity);
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
            IsEnabled = true,
            ConfigSchemaJson = request.ConfigSchemaJson,
            ConfigValuesJson = request.ConfigValuesJson ?? "{}",
            ReadmeMarkdown = request.ReadmeMarkdown,
            InstalledAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Extensions.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDetail(entity);
    }

    public async Task<ExtensionDetailResponse> UploadPackageAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw HttpApiException.BadRequest("Extension package must be a .zip file.");

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
        var manifestEntry = archive.GetEntry("extension.json") ?? archive.GetEntry("manifest.json");

        if (manifestEntry == null)
            throw HttpApiException.BadRequest("The package contains neither 'extension.json' nor 'manifest.json'.");

        ExtensionInstallRequest? manifest;
        using (var stream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync<ExtensionInstallRequest>(stream, cancellationToken: cancellationToken);
        }

        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Identifier))
            throw HttpApiException.BadRequest("Invalid manifest file in the extension.");

        var readmeEntry = archive.GetEntry("README.md") ?? archive.GetEntry("readme.md");
        if (readmeEntry != null)
        {
            using var reader = new StreamReader(readmeEntry.Open());
            manifest = manifest with { ReadmeMarkdown = await reader.ReadToEndAsync(cancellationToken) };
        }

        return await InstallAsync(manifest, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Extensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
            throw HttpApiException.NotFound("Extension not found.");

        _context.Extensions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted extension {Identifier}", entity.Identifier);
    }

    private async Task EnsureDefaultsAsync(CancellationToken cancellationToken)
    {
        if (await _context.Extensions.AnyAsync(cancellationToken))
            return;

        var defaults = new List<ExtensionEntity>
        {
            new()
            {
                Identifier = "discord-notifier",
                Name = "Discord Webhook Notifier",
                Description = "Sends automatic notifications about server starts, stops, crashes and security events directly to Discord channels.",
                Version = "1.2.0",
                Author = "Midnight Team",
                Category = "Integration",
                Icon = "MessageIcon",
                WebsiteUrl = "https://github.com/Midnight-Panel/extension-discord",
                IsEnabled = true,
                ConfigSchemaJson = """
                {
                  "type": "object",
                  "properties": {
                    "webhookUrl": { "type": "string", "title": "Discord Webhook URL", "description": "The target Discord webhook URL" },
                    "notifyOnCrash": { "type": "boolean", "title": "Send crash alerts", "default": true },
                    "notifyOnBackup": { "type": "boolean", "title": "Report backup status", "default": true }
                  }
                }
                """,
                ConfigValuesJson = """{"webhookUrl": "", "notifyOnCrash": true, "notifyOnBackup": true}""",
                ReadmeMarkdown = "# Discord Webhook Notifier\n\nSeamlessly integrate your game server panel with your Discord server.",
                InstalledAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Identifier = "backup-scheduler",
                Name = "Automated Backup Scheduler",
                Description = "Advanced cron-based schedules for periodic server backups, including optional Cloudflare R2 / S3 cloud sync.",
                Version = "2.0.1",
                Author = "Midnight Core",
                Category = "Administration",
                Icon = "FloppyDiscIcon",
                WebsiteUrl = "https://midnightpanel.com/docs/extensions/backups",
                IsEnabled = true,
                ConfigSchemaJson = """
                {
                  "type": "object",
                  "properties": {
                    "cronExpression": { "type": "string", "title": "Default cron expression", "default": "0 3 * * *" },
                    "keepDays": { "type": "number", "title": "Retention (days)", "default": 14 }
                  }
                }
                """,
                ConfigValuesJson = """{"cronExpression": "0 3 * * *", "keepDays": 14}""",
                ReadmeMarkdown = "# Automated Backup Scheduler\n\nBack up your game server data fully automatically.",
                InstalledAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Identifier = "audit-log-exporter",
                Name = "Security Audit Exporter",
                Description = "Exports login attempts, 2FA activations and administrative changes to SIEM systems and external log servers.",
                Version = "1.0.4",
                Author = "Security SIG",
                Category = "Security",
                Icon = "LockIcon",
                WebsiteUrl = "https://midnightpanel.com",
                IsEnabled = false,
                ConfigSchemaJson = """
                {
                  "type": "object",
                  "properties": {
                    "exportFormat": { "type": "string", "enum": ["JSON", "CEF", "Syslog"], "default": "JSON" },
                    "remoteSyslogHost": { "type": "string", "title": "Remote Syslog Server" }
                  }
                }
                """,
                ConfigValuesJson = """{"exportFormat": "JSON", "remoteSyslogHost": ""}""",
                ReadmeMarkdown = "# Security Audit Exporter\n\nImproved compliance and traceability for Midnight Panel.",
                InstalledAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Identifier = "resource-analytics",
                Name = "Node & Resource Analytics",
                Description = "Creates detailed performance reports and long-term metrics on CPU, RAM and disk usage.",
                Version = "1.1.0",
                Author = "Midnight Metrics",
                Category = "Tools",
                Icon = "PieChartIcon",
                WebsiteUrl = "https://midnightpanel.com",
                IsEnabled = true,
                ConfigSchemaJson = """
                {
                  "type": "object",
                  "properties": {
                    "retentionDays": { "type": "number", "title": "Metrics retention (days)", "default": 30 },
                    "samplingIntervalSec": { "type": "number", "title": "Interval (seconds)", "default": 60 }
                  }
                }
                """,
                ConfigValuesJson = """{"retentionDays": 30, "samplingIntervalSec": 60}""",
                ReadmeMarkdown = "# Node & Resource Analytics\n\nPrecise analytics for your game server infrastructure.",
                InstalledAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Identifier = "subdomain-manager",
                Name = "Cloudflare Subdomain Automator",
                Description = "Automatically connects Cloudflare DNS to new game servers (e.g. s1.arwenagg.de -> node IP:port).",
                Version = "1.3.2",
                Author = "Community",
                Category = "Integration",
                Icon = "SparkIcon",
                WebsiteUrl = "https://github.com/Midnight-Panel/extension-cloudflare",
                IsEnabled = true,
                ConfigSchemaJson = """
                {
                  "type": "object",
                  "properties": {
                    "apiToken": { "type": "string", "title": "Cloudflare API Token" },
                    "zoneId": { "type": "string", "title": "Zone ID" },
                    "baseDomain": { "type": "string", "title": "Base domain", "default": "arwenagg.de" }
                  }
                }
                """,
                ConfigValuesJson = """{"apiToken": "", "zoneId": "", "baseDomain": "arwenagg.de"}""",
                ReadmeMarkdown = "# Cloudflare Subdomain Automator\n\nNever create DNS records for players by hand again.",
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
        IsEnabled = entity.IsEnabled,
        InstalledAt = entity.InstalledAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static ExtensionDetailResponse MapToDetail(ExtensionEntity entity) => new()
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
        IsEnabled = entity.IsEnabled,
        ConfigSchemaJson = entity.ConfigSchemaJson,
        ConfigValuesJson = entity.ConfigValuesJson,
        ReadmeMarkdown = entity.ReadmeMarkdown,
        RequiredPermissions = [],
        InstalledAt = entity.InstalledAt,
        UpdatedAt = entity.UpdatedAt
    };
}
