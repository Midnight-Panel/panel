using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Midnight.ApiServer.Core.Http;

namespace Midnight.ApiServer.Features.Branding.Services;

/// <summary>
/// Validiert Hex-Farben und berechnet Kontrastverhältnisse gemäß WCAG 2.1 Richtlinien.
/// </summary>
public static class ColorContrastValidator
{
    private static readonly Regex HexColorRegex = new(@"^#([0-9A-Fa-f]{6})$", RegexOptions.Compiled);

    public static void ValidateHex(string color, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(color) || !HexColorRegex.IsMatch(color))
            throw HttpApiException.BadRequest($"Ungültiger Farbwert für {propertyName}. Erwartetes Format: #RRGGBB");
    }

    /// <summary>
    /// Berechnet die relative Helligkeit (Relative Luminance) einer Farbe nach WCAG 2.1.
    /// </summary>
    public static double CalculateLuminance(string hexColor)
    {
        ValidateHex(hexColor, "Color");

        var r = int.Parse(hexColor.Substring(1, 2), NumberStyles.HexNumber) / 255.0;
        var g = int.Parse(hexColor.Substring(3, 2), NumberStyles.HexNumber) / 255.0;
        var b = int.Parse(hexColor.Substring(5, 2), NumberStyles.HexNumber) / 255.0;

        double Adjust(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        return 0.2126 * Adjust(r) + 0.7152 * Adjust(g) + 0.0722 * Adjust(b);
    }

    /// <summary>
    /// Berechnet das Kontrastverhältnis zweier Farben nach WCAG 2.1 (1:1 bis 21:1).
    /// </summary>
    public static double CalculateContrastRatio(string color1, string color2)
    {
        var l1 = CalculateLuminance(color1);
        var l2 = CalculateLuminance(color2);

        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);

        return (lighter + 0.05) / (darker + 0.05);
    }
}
