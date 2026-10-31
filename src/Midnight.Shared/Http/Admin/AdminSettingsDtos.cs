namespace Midnight.Shared.Http.Admin;

/// <summary>
/// Custom footer link shown in panel footer.
/// </summary>
public class FooterLinkItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Label { get; set; } = "";
    public string Url { get; set; } = "";
    public bool NewTab { get; set; } = true;

    /// <summary>Only http(s) and mailto links or paths on the panel itself, never javascript: or data: urls</summary>
    public static bool IsSafeUrl(string? url)
    {
        url = url?.Trim();

        if (string.IsNullOrEmpty(url))
            return false;

        if (url.StartsWith('/'))
            return !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal);

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto);
    }

    /// <summary>Absolute http(s) url, e.g. the public url of the panel used in mails and the demon config</summary>
    public static bool IsHttpUrl(string? url)
        => Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

/// <summary>
/// Response containing system and panel settings for administrators.
/// </summary>
public class AdminSettingsResponse
{
    public string AppName { get; set; } = "Midnight";
    public string PublicUrl { get; set; } = "http://localhost:5220";
    public bool EnableRegistration { get; set; } = true;
    public bool UserApiEnabled { get; set; } = true;
    public bool MaintenanceMode { get; set; }
    public bool ServerCreationEnabled { get; set; } = true;
    public bool TelemetryEnabled { get; set; }
    public List<FooterLinkItem> FooterLinks { get; set; } = [];
}

/// <summary>
/// Request model to update system settings.
/// </summary>
public class AdminSettingsUpdateRequest
{
    public string AppName { get; set; } = "Midnight";
    public string PublicUrl { get; set; } = "http://localhost:5220";
    public bool EnableRegistration { get; set; } = true;
    public bool UserApiEnabled { get; set; } = true;
    public bool MaintenanceMode { get; set; }
    public bool ServerCreationEnabled { get; set; } = true;
    public bool TelemetryEnabled { get; set; }
    public List<FooterLinkItem> FooterLinks { get; set; } = [];
}
