using System;

namespace Midnight.ApiServer.Features.Branding.Entities;

/// <summary>
/// Singleton configuration table for the panel branding (Id = 1).
/// </summary>
public class BrandingSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Display name of the panel (browser tab, header, mails).</summary>
    public string PanelName { get; set; } = "Midnight Panel";

    /// <summary>File name of the logo for the light theme (relative to storage).</summary>
    public string? LogoLightFileName { get; set; }

    /// <summary>File name of the logo for the dark theme.</summary>
    public string? LogoDarkFileName { get; set; }

    /// <summary>File name of the favicon (.ico, .png, .svg).</summary>
    public string? FaviconFileName { get; set; }

    /// <summary>Hex value of the primary color, e.g. #465FFF.</summary>
    public string PrimaryColor { get; set; } = "#465FFF";

    /// <summary>Hex value of the accent color, e.g. #7C3AED.</summary>
    public string AccentColor { get; set; } = "#7C3AED";

    /// <summary>Background mode for auth pages: 'none', 'gradient' or 'image'.</summary>
    public string LoginBackgroundType { get; set; } = "gradient";

    /// <summary>CSS gradient or file name of the background image.</summary>
    public string? LoginBackgroundValue { get; set; } = "linear-gradient(135deg, #0e0e10 0%, #1c1c22 50%, #0e0e10 100%)";

    /// <summary>Admin-defined custom CSS (sanitized and validated).</summary>
    public string? CustomCss { get; set; }

    /// <summary>Timestamp of the last change.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
