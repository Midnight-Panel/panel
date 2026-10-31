using System.ComponentModel.DataAnnotations;

namespace Midnight.ApiServer.Features.Extensions.Entities;

public class ExtensionEntity
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string Identifier { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Version { get; set; } = "1.0.0";

    [MaxLength(128)]
    public string Author { get; set; } = "Midnightpanel";

    [MaxLength(64)]
    public string Category { get; set; } = "General";

    [MaxLength(64)]
    public string? Icon { get; set; }

    [MaxLength(512)]
    public string? WebsiteUrl { get; set; }

    [MaxLength(32)]
    public string InstallSource { get; set; } = "file";

    [MaxLength(1024)]
    public string? SourceUrl { get; set; }

    [MaxLength(256)]
    public string? PackageFileName { get; set; }

    [MaxLength(512)]
    public string? StoragePath { get; set; }

    public bool IsEnabled { get; set; } = true;

    [MaxLength(32768)]
    public string? ConfigSchemaJson { get; set; }

    [MaxLength(32768)]
    public string? ConfigValuesJson { get; set; }

    public string? ReadmeMarkdown { get; set; }

    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
