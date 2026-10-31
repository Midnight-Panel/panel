using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Midnight.ApiServer.Core.Http;

namespace Midnight.ApiServer.Features.Branding.Services;

/// <summary>
/// Sanitizes and validates custom CSS to guarantee that the footer can never be
/// hidden, covered or made unreadable.
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

    /// <summary>CSS escapes like "@\69mport" or "f\6f oter" would otherwise bypass the checks below</summary>
    private static readonly Regex CssEscapeRegex = new(
        @"\\([0-9a-fA-F]{1,6})\s?|\\(.)",
        RegexOptions.Singleline | RegexOptions.Compiled
    );

    public static string Sanitize(string? css)
    {
        if (string.IsNullOrWhiteSpace(css))
            return string.Empty;

        if (css.Length > 32768)
            throw HttpApiException.BadRequest("Custom CSS must not exceed 32 KB.");

        // All checks run on the unescaped css, the original is stored
        var decoded = DecodeEscapes(css);

        if (DangerousExpressionRegex.IsMatch(css) || DangerousExpressionRegex.IsMatch(decoded))
            throw HttpApiException.BadRequest("Custom CSS contains forbidden statements (@import, javascript: or expressions).");

        // The css ends up in a <style> element, it must not be able to close it
        if (decoded.Contains('<'))
            throw HttpApiException.BadRequest("Custom CSS must not contain HTML tags.");

        var withoutComments = Regex.Replace(decoded, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        foreach (var selector in ForbiddenSelectors)
        {
            var pattern = $@"(^|[\s,{{}}]){Regex.Escape(selector)}([\s,{{:]|$)";
            if (Regex.IsMatch(withoutComments, pattern, RegexOptions.IgnoreCase))
            {
                throw HttpApiException.BadRequest(
                    $"Modifying the mandatory branding is not allowed: selector '{selector}' is blocked.");
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
                        "Global CSS rules must not contain properties that hide or overlay elements.");
                }
            }
        }

        return css.Trim();
    }

    private static string DecodeEscapes(string css) => CssEscapeRegex.Replace(css, match =>
    {
        if (!match.Groups[1].Success)
            return match.Groups[2].Value;

        var codePoint = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return codePoint is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF)
            ? char.ConvertFromUtf32(codePoint)
            : "\uFFFD";
    });
}
