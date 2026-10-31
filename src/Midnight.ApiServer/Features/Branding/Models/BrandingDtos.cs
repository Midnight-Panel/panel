using System;

namespace Midnight.ApiServer.Features.Branding.Models;

/// <summary>
/// Unveränderbarer Footer-Bestandteil, der serverseitig erzwungen wird.
/// </summary>
public record PoweredByInfo(
    string Text,
    string Url,
    bool Enforced = true
);

/// <summary>
/// Öffentliche Branding-Informationen für alle Clients.
/// </summary>
public record BrandingResponse(
    string PanelName,
    string? LogoLightUrl,
    string? LogoDarkUrl,
    string? FaviconUrl,
    string PrimaryColor,
    string AccentColor,
    string LoginBackgroundType,
    string? LoginBackgroundValue,
    string? CustomCss,
    PoweredByInfo PoweredBy,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// Admin-Anfrage zur Aktualisierung des Brandings.
/// Das Feld 'PoweredBy' existiert hier bewusst nicht.
/// </summary>
public record BrandingUpdateRequest(
    string PanelName,
    string PrimaryColor,
    string AccentColor,
    string LoginBackgroundType,
    string? LoginBackgroundValue,
    string? CustomCss
);

public enum LogoThemeType
{
    Light,
    Dark
}
