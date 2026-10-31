namespace Midnight.Shared.Http.Extensions;

public record ExtensionSummaryResponse
{
    public int Id { get; init; }
    public string Identifier { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "Midnightpanel";
    public string Category { get; init; } = "General";
    public string? WebsiteUrl { get; init; }
    public string? Icon { get; init; }
    public bool IsEnabled { get; init; } = true;
    public DateTimeOffset InstalledAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public record ExtensionDetailResponse : ExtensionSummaryResponse
{
    public string? ConfigSchemaJson { get; init; }
    public string? ConfigValuesJson { get; init; }
    public string? ReadmeMarkdown { get; init; }
    public List<string> RequiredPermissions { get; init; } = [];
}

public record ExtensionInstallRequest
{
    public string Identifier { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "Midnightpanel";
    public string Category { get; init; } = "General";
    public string? WebsiteUrl { get; init; }
    public string? Icon { get; init; }
    public string? ConfigSchemaJson { get; init; }
    public string? ConfigValuesJson { get; init; }
    public string? ReadmeMarkdown { get; init; }
}

public record ExtensionConfigUpdateRequest
{
    public string ConfigValuesJson { get; init; } = "{}";
}

public record ExtensionToggleRequest
{
    public bool IsEnabled { get; init; }
}
