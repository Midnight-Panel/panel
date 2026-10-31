using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Midnight.ApiServer.Core.Http;

namespace Midnight.ApiServer.Features.Branding.Services;

/// <summary>
/// Strictly strips uploaded SVGs of all XSS vectors, JavaScript and external references.
/// </summary>
public static class SvgSanitizer
{
    // Compared against the local name (case-insensitive), so prefixes like "svg:script" or "foreignObject" are caught too.
    // set/animate* can rewrite attributes like href at runtime and are removed as well.
    private static readonly HashSet<string> DangerousTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "iframe", "object", "embed", "applet", "foreignobject", "audio", "video", "use", "image",
        "feimage", "handler", "listener", "set", "animate", "animatemotion", "animatetransform", "animatecolor"
    };

    /// <summary>Matches @import and url() references that don't point to a fragment of the document itself</summary>
    private static readonly Regex ExternalReferenceRegex = new(
        @"@import|url\s*\(\s*['""]?\s*(?![\s'""#])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static byte[] Sanitize(ReadOnlySpan<byte> svgBytes)
    {
        var rawXml = Encoding.UTF8.GetString(svgBytes);

        if (rawXml.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
            rawXml.Contains("data:text/html", StringComparison.OrdinalIgnoreCase))
        {
            throw HttpApiException.BadRequest("SVG contains unsafe JavaScript references.");
        }

        var xmlDoc = new XmlDocument
        {
            XmlResolver = null
        };

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        try
        {
            using var textReader = new StringReader(rawXml);
            using var reader = XmlReader.Create(textReader, settings);
            xmlDoc.Load(reader);
        }
        catch (Exception ex)
        {
            throw HttpApiException.BadRequest($"Invalid SVG format: {ex.Message}");
        }

        if (xmlDoc.DocumentElement == null || !xmlDoc.DocumentElement.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
            throw HttpApiException.BadRequest("Invalid SVG format: the root element must be <svg>.");

        SanitizeNode(xmlDoc);

        using var memoryStream = new MemoryStream();
        using (var writer = XmlWriter.Create(memoryStream, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = false }))
        {
            xmlDoc.Save(writer);
        }

        return memoryStream.ToArray();
    }

    private static void SanitizeNode(XmlNode node)
    {
        // Copy, the child list is modified while iterating
        foreach (var child in node.ChildNodes.Cast<XmlNode>().ToList())
        {
            switch (child)
            {
                // e.g. <?xml-stylesheet href="..."?> can load external stylesheets
                case XmlProcessingInstruction:
                    node.RemoveChild(child);
                    break;

                case XmlElement element when DangerousTags.Contains(element.LocalName):
                    node.RemoveChild(child);
                    break;

                case XmlElement element when element.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase) &&
                                             ExternalReferenceRegex.IsMatch(element.InnerText):
                    node.RemoveChild(child);
                    break;

                case XmlElement element:
                    SanitizeAttributes(element);
                    SanitizeNode(element);
                    break;
            }
        }
    }

    private static void SanitizeAttributes(XmlElement element)
    {
        for (var i = element.Attributes.Count - 1; i >= 0; i--)
        {
            var attr = element.Attributes[i];
            var name = attr.LocalName.ToLowerInvariant();
            var value = attr.Value.Trim();

            // Event handlers (onload, onclick, ...)
            var remove = name.StartsWith("on", StringComparison.Ordinal);

            // href and xlink:href may only reference elements of the document itself. Everything else
            // (javascript:, data:, http:, obfuscated schemes with whitespace or entities) is dropped.
            if (name == "href" && !value.StartsWith('#'))
                remove = true;

            // External resources in presentation attributes or inline styles, e.g. fill="url(https://...)"
            if (ExternalReferenceRegex.IsMatch(value))
                remove = true;

            if (remove)
                element.Attributes.RemoveAt(i);
        }
    }
}
