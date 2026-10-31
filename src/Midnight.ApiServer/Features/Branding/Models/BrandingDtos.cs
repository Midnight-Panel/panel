using System;
using System.Collections.Generic;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.Branding.Models;

/// <summary>
/// Immutable footer part, enforced on the server.
/// </summary>
public record PoweredByInfo(
    string Text,
    string Url,
    bool Enforced = true
);

/// <summary>
/// Public branding information for all clients.
/// </summary>
public record BrandingResponse(
    string PanelName,
    string? Slogan,
    string? PanelUrl,
    string? DefaultLanguage,
    string? TimeZone,
    List<FooterLinkItem>? FooterLinks,
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
/// Admin request to update the branding.
/// The 'PoweredBy' field is intentionally absent here.
/// </summary>
public record BrandingUpdateRequest(
    string PanelName,
    string? Slogan,
    string? PanelUrl,
    string? DefaultLanguage,
    string? TimeZone,
    List<FooterLinkItem>? FooterLinks,
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
