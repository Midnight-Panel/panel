using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Branding.Entities;
using Midnight.ApiServer.Features.Branding.Models;

namespace Midnight.ApiServer.Features.Branding.Services;

public class BrandingService : IBrandingService
{
    private const string CacheKey = "midnight_branding_settings";
    private const long MaxFileSize = 2 * 1024 * 1024; // 2 MB
    private const string DefaultPoweredByUrl = "https://midnightpanel.com";

    private readonly DataContext _context;
    private readonly IMemoryCache _cache;
    private readonly StoragePaths _storagePaths;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BrandingService> _logger;
    private readonly string _brandingDirectory;

    public BrandingService(
        DataContext context,
        IMemoryCache cache,
        StoragePaths storagePaths,
        IConfiguration configuration,
        ILogger<BrandingService> logger)
    {
        _context = context;
        _cache = cache;
        _storagePaths = storagePaths;
        _configuration = configuration;
        _logger = logger;
        _brandingDirectory = storagePaths.Branding;
    }

    public async Task<BrandingResponse> GetAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out BrandingResponse? cached) && cached != null)
            return cached;

        var entity = await GetOrCreateSettingsAsync(cancellationToken);
        var response = MapToResponse(entity);

        _cache.Set(CacheKey, response, TimeSpan.FromHours(1));
        return response;
    }

    public async Task<BrandingResponse> UpdateAsync(BrandingUpdateRequest request, int actorUserId, CancellationToken cancellationToken)
    {
        ColorContrastValidator.ValidateHex(request.PrimaryColor, nameof(request.PrimaryColor));
        ColorContrastValidator.ValidateHex(request.AccentColor, nameof(request.AccentColor));

        var sanitizedCss = CssSanitizer.Sanitize(request.CustomCss);

        var entity = await GetOrCreateSettingsAsync(cancellationToken);
        entity.PanelName = string.IsNullOrWhiteSpace(request.PanelName) ? "Midnight Panel" : request.PanelName.Trim();
        entity.PrimaryColor = request.PrimaryColor.ToUpperInvariant();
        entity.AccentColor = request.AccentColor.ToUpperInvariant();
        entity.LoginBackgroundType = request.LoginBackgroundType switch
        {
            "image" => "image",
            "none" => "none",
            _ => "gradient"
        };
        entity.LoginBackgroundValue = request.LoginBackgroundValue?.Trim();
        entity.CustomCss = sanitizedCss;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey);

        _logger.LogInformation("Audit: User {UserId} hat die Branding-Einstellungen aktualisiert.", actorUserId);

        return MapToResponse(entity);
    }

    public async Task<BrandingResponse> UploadLogoAsync(LogoThemeType theme, Stream content, string originalName, int actorUserId, CancellationToken cancellationToken)
    {
        var fileName = await SaveUploadedFileAsync(content, originalName, "logo", cancellationToken);
        var entity = await GetOrCreateSettingsAsync(cancellationToken);

        if (theme == LogoThemeType.Light)
        {
            DeleteOldFile(entity.LogoLightFileName);
            entity.LogoLightFileName = fileName;
        }
        else
        {
            DeleteOldFile(entity.LogoDarkFileName);
            entity.LogoDarkFileName = fileName;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey);

        _logger.LogInformation("Audit: User {UserId} hat das Logo ({Theme}) aktualisiert: {FileName}", actorUserId, theme, fileName);

        return MapToResponse(entity);
    }

    public async Task<BrandingResponse> DeleteLogoAsync(LogoThemeType theme, int actorUserId, CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateSettingsAsync(cancellationToken);
        if (theme == LogoThemeType.Light)
        {
            DeleteOldFile(entity.LogoLightFileName);
            entity.LogoLightFileName = null;
        }
        else
        {
            DeleteOldFile(entity.LogoDarkFileName);
            entity.LogoDarkFileName = null;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey);

        _logger.LogInformation("Audit: User {UserId} hat das Logo ({Theme}) gelöscht.", actorUserId, theme);
        return MapToResponse(entity);
    }

    public async Task<BrandingResponse> UploadFaviconAsync(Stream content, string originalName, int actorUserId, CancellationToken cancellationToken)
    {
        var fileName = await SaveUploadedFileAsync(content, originalName, "favicon", cancellationToken);
        var entity = await GetOrCreateSettingsAsync(cancellationToken);

        DeleteOldFile(entity.FaviconFileName);
        entity.FaviconFileName = fileName;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey);

        _logger.LogInformation("Audit: User {UserId} hat das Favicon aktualisiert: {FileName}", actorUserId, fileName);

        return MapToResponse(entity);
    }

    public async Task<BrandingResponse> DeleteFaviconAsync(int actorUserId, CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateSettingsAsync(cancellationToken);
        DeleteOldFile(entity.FaviconFileName);
        entity.FaviconFileName = null;

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey);

        _logger.LogInformation("Audit: User {UserId} hat das Favicon gelöscht.", actorUserId);
        return MapToResponse(entity);
    }

    public async Task<BrandingResponse> ResetToDefaultsAsync(int actorUserId, CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateSettingsAsync(cancellationToken);

        DeleteOldFile(entity.LogoLightFileName);
        DeleteOldFile(entity.LogoDarkFileName);
        DeleteOldFile(entity.FaviconFileName);

        entity.PanelName = "Midnight Panel";
        entity.PrimaryColor = "#465FFF";
        entity.AccentColor = "#7C3AED";
        entity.LoginBackgroundType = "gradient";
        entity.LoginBackgroundValue = "linear-gradient(135deg, #0F172A 0%, #1E1B4B 50%, #0F172A 100%)";
        entity.CustomCss = null;
        entity.LogoLightFileName = null;
        entity.LogoDarkFileName = null;
        entity.FaviconFileName = null;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey);

        _logger.LogInformation("Audit: User {UserId} hat das Branding auf Werkseinstellungen zurückgesetzt.", actorUserId);

        return MapToResponse(entity);
    }

    public async Task<(string Path, string ContentType)?> GetFileAsync(string fileName, CancellationToken cancellationToken)
    {
        var safeFileName = Path.GetFileName(fileName);
        var path = Path.Combine(_brandingDirectory, safeFileName);

        if (!File.Exists(path))
            return null;

        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };

        return await Task.FromResult<(string Path, string ContentType)?>((path, contentType));
    }

    private async Task<string> SaveUploadedFileAsync(Stream content, string originalName, string prefix, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, cancellationToken);

        if (memoryStream.Length == 0 || memoryStream.Length > MaxFileSize)
            throw HttpApiException.BadRequest("Die Datei muss zwischen 1 Byte und 2 MB groß sein.");

        var rawBytes = memoryStream.ToArray();
        var extension = DetectAndValidateExtension(rawBytes, originalName);

        byte[] finalBytes = rawBytes;
        if (extension == ".svg")
        {
            finalBytes = SvgSanitizer.Sanitize(rawBytes);
        }

        Directory.CreateDirectory(_brandingDirectory);
        var fileName = $"{prefix}_{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_brandingDirectory, fileName);

        await File.WriteAllBytesAsync(fullPath, finalBytes, cancellationToken);
        return fileName;
    }

    private static string DetectAndValidateExtension(ReadOnlySpan<byte> bytes, string originalName)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ".png";

        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return ".webp";

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x01 && bytes[3] == 0x00)
            return ".ico";

        var textHeader = System.Text.Encoding.UTF8.GetString(bytes.Slice(0, Math.Min(bytes.Length, 512))).Trim();
        if (textHeader.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            return ".svg";

        throw HttpApiException.BadRequest("Nicht unterstütztes Dateiformat. Erlaubt sind PNG, WebP, SVG und ICO.");
    }

    private void DeleteOldFile(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var fullPath = Path.Combine(_brandingDirectory, Path.GetFileName(fileName));
        if (File.Exists(fullPath))
        {
            try { File.Delete(fullPath); } catch { /* Ignore cleanup errors */ }
        }
    }

    private async Task<BrandingSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _context.BrandingSettings.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (settings == null)
        {
            settings = new BrandingSettings { Id = 1, UpdatedAt = DateTimeOffset.UtcNow };
            _context.BrandingSettings.Add(settings);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return settings;
    }

    private BrandingResponse MapToResponse(BrandingSettings entity)
    {
        var projectUrl = _configuration["Branding:PoweredByUrl"] ?? DefaultPoweredByUrl;

        return new BrandingResponse(
            PanelName: entity.PanelName,
            LogoLightUrl: entity.LogoLightFileName != null ? $"/api/branding/files/{entity.LogoLightFileName}" : null,
            LogoDarkUrl: entity.LogoDarkFileName != null ? $"/api/branding/files/{entity.LogoDarkFileName}" : null,
            FaviconUrl: entity.FaviconFileName != null ? $"/api/branding/files/{entity.FaviconFileName}" : null,
            PrimaryColor: entity.PrimaryColor,
            AccentColor: entity.AccentColor,
            LoginBackgroundType: entity.LoginBackgroundType,
            LoginBackgroundValue: entity.LoginBackgroundValue,
            CustomCss: entity.CustomCss,
            PoweredBy: new PoweredByInfo("Powered by Midnight Panel", projectUrl, Enforced: true),
            UpdatedAt: entity.UpdatedAt
        );
    }
}
