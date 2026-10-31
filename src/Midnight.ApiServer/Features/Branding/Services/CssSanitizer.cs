using System;
using System.Text.RegularExpressions;
using Midnight.ApiServer.Core.Http;

namespace Midnight.ApiServer.Features.Branding.Services;

/// <summary>
/// Bereinigt und validiert Custom CSS, um zu garantieren, dass der Footer niemals
/// ausgeblendet, überdeckt oder unleserlich gemacht werden kann.
/// </summary>
public static class CssSanitizer
{
    private static readonly string[] ForbiddenSelectors =
    [
        "footer",
        "#mp-powered-by",
        ".mp-powered-by",
        "[data-mp-footer]",
        "[data-mp-enforced]",
        "#mp-immutable-footer",
        ".mp-immutable-footer",
        "mp-footer",
        "body > footer",
        "#root > footer"
    ];

    private static readonly Regex DangerousPropertyRegex = new(
        @"(display\s*:\s*none|visibility\s*:\s*hidden|opacity\s*:\s*0|font-size\s*:\s*0|height\s*:\s*0|max-height\s*:\s*0|transform\s*:\s*scale\(0\)|clip-path\s*:|position\s*:\s*fixed|z-index\s*:\s*99999|content\s*:)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex DangerousExpressionRegex = new(
        @"(expression\(|behavior\s*:|@import|javascript\s*:|data\s*:)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public static string Sanitize(string? css)
    {
        if (string.IsNullOrWhiteSpace(css))
            return string.Empty;

        if (css.Length > 32768)
            throw HttpApiException.BadRequest("Custom CSS darf maximal 32 KB groß sein.");

        if (DangerousExpressionRegex.IsMatch(css))
            throw HttpApiException.BadRequest("Custom CSS enthält unzulässige Anweisungen (@import, javascript: oder expressions).");

        var withoutComments = Regex.Replace(css, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        foreach (var selector in ForbiddenSelectors)
        {
            var pattern = $@"(^|[\s,{{}}]){Regex.Escape(selector)}([\s,{{:]|$)";
            if (Regex.IsMatch(withoutComments, pattern, RegexOptions.IgnoreCase))
            {
                throw HttpApiException.BadRequest(
                    $"Manipulation des Pflicht-Brandings nicht erlaubt: Selektor '{selector}' ist gesperrt.");
            }
        }

        var rules = withoutComments.Split('}');
        foreach (var rule in rules)
        {
            var parts = rule.Split('{');
            if (parts.Length == 2)
            {
                var selectorPart = parts[0].Trim().ToLowerInvariant();
                var bodyPart = parts[1];

                if ((selectorPart.Contains("body") || selectorPart.Contains("html") || selectorPart.Contains("*"))
                    && DangerousPropertyRegex.IsMatch(bodyPart))
                {
                    throw HttpApiException.BadRequest(
                        "Globale CSS-Regeln dürfen keine Ausblendungs- oder Überlagerungseigenschaften enthalten.");
                }
            }
        }

        return css.Trim();
    }
}
