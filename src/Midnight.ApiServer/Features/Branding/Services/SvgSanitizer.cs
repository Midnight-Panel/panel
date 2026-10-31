using System;
using System.IO;
using System.Text;
using System.Xml;
using Midnight.ApiServer.Core.Http;

namespace Midnight.ApiServer.Features.Branding.Services;

/// <summary>
/// Bereinigt hochgeladene SVGs strikt von allen XSS-Vektoren, JavaScript und externen Referenzen.
/// </summary>
public static class SvgSanitizer
{
    private static readonly string[] DangerousTags =
    [
        "script", "iframe", "object", "embed", "applet", "foreignobject", "audio", "video", "use", "image"
    ];

    public static byte[] Sanitize(ReadOnlySpan<byte> svgBytes)
    {
        var rawXml = Encoding.UTF8.GetString(svgBytes);

        if (rawXml.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
            rawXml.Contains("data:text/html", StringComparison.OrdinalIgnoreCase))
        {
            throw HttpApiException.BadRequest("SVG enthält unsichere JavaScript-Referenzen.");
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
            throw HttpApiException.BadRequest($"Ungültiges SVG-Format: {ex.Message}");
        }

        foreach (var tagName in DangerousTags)
        {
            var nodes = xmlDoc.GetElementsByTagName(tagName);
            for (var i = nodes.Count - 1; i >= 0; i--)
            {
                var node = nodes[i];
                node?.ParentNode?.RemoveChild(node);
            }
        }

        SanitizeNode(xmlDoc.DocumentElement);

        using var memoryStream = new MemoryStream();
        using (var writer = XmlWriter.Create(memoryStream, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = false }))
        {
            xmlDoc.Save(writer);
        }

        return memoryStream.ToArray();
    }

    private static void SanitizeNode(XmlNode? node)
    {
        if (node == null) return;

        if (node.Attributes != null)
        {
            for (var i = node.Attributes.Count - 1; i >= 0; i--)
            {
                var attr = node.Attributes[i];
                var name = attr.Name.ToLowerInvariant();
                var value = attr.Value.ToLowerInvariant();

                if (name.StartsWith("on") ||
                    name.Contains("href") && (value.StartsWith("http:") || value.StartsWith("https:") || value.StartsWith("javascript:")))
                {
                    node.Attributes.RemoveAt(i);
                }
            }
        }

        foreach (XmlNode child in node.ChildNodes)
        {
            SanitizeNode(child);
        }
    }
}
