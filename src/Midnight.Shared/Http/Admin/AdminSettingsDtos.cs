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
