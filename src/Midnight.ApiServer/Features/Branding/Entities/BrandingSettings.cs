using System;

namespace Midnight.ApiServer.Features.Branding.Entities;

/// <summary>
/// Singleton-Konfigurationstabelle für das Panel-Branding (Id = 1).
/// </summary>
public class BrandingSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Angezeigter Name des Panels (Browser-Tab, Header, Mails).</summary>
    public string PanelName { get; set; } = "Midnight Panel";

    /// <summary>Dateiname des Logos für das helle Farbschema (relativ zum Storage).</summary>
    public string? LogoLightFileName { get; set; }

    /// <summary>Dateiname des Logos für das dunkle Farbschema.</summary>
    public string? LogoDarkFileName { get; set; }

    /// <summary>Dateiname des Favicons (.ico, .png, .svg).</summary>
    public string? FaviconFileName { get; set; }

    /// <summary>Hex-Farbwert der Primärfarbe (z. B. #465FFF).</summary>
    public string PrimaryColor { get; set; } = "#465FFF";

    /// <summary>Hex-Farbwert der Akzentfarbe (z. B. #7C3AED).</summary>
    public string AccentColor { get; set; } = "#7C3AED";

    /// <summary>Hintergrund-Modus für Auth-Seiten: 'none', 'gradient' oder 'image'.</summary>
    public string LoginBackgroundType { get; set; } = "gradient";

    /// <summary>CSS-Gradient oder Dateiname des Hintergrundbilds.</summary>
    public string? LoginBackgroundValue { get; set; } = "linear-gradient(135deg, #0F172A 0%, #1E1B4B 50%, #0F172A 100%)";

    /// <summary>Admin-definiertes Custom CSS (bereinigt und validiert).</summary>
    public string? CustomCss { get; set; }

    /// <summary>Zeitstempel der letzten Änderung.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
