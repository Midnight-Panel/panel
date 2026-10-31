using System;
using System.Text;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Branding.Services;
using Xunit;

namespace Midnight.Panel.Tests;

public class BrandingTests
{
    [Fact]
    public void CssSanitizer_BlocksFooterDisplayNone()
    {
        var maliciousCss = "footer { display: none !important; }";
        Assert.Throws<HttpApiException>(() => CssSanitizer.Sanitize(maliciousCss));
    }

    [Fact]
    public void CssSanitizer_BlocksPoweredByIdTargeting()
    {
        var maliciousCss = "#mp-powered-by { visibility: hidden; }";
        Assert.Throws<HttpApiException>(() => CssSanitizer.Sanitize(maliciousCss));
    }

    [Fact]
    public void CssSanitizer_BlocksImmutableFooterTargeting()
    {
        var maliciousCss = "#mp-immutable-footer { opacity: 0; }";
        Assert.Throws<HttpApiException>(() => CssSanitizer.Sanitize(maliciousCss));
    }

    [Fact]
    public void CssSanitizer_BlocksGlobalHideProperties()
    {
        var maliciousCss = "body { display: none; }";
        Assert.Throws<HttpApiException>(() => CssSanitizer.Sanitize(maliciousCss));
    }

    [Fact]
    public void CssSanitizer_AllowsHarmlessStyles()
    {
        var safeCss = ".my-custom-card { border-radius: 12px; background: #1E293B; }";
        var result = CssSanitizer.Sanitize(safeCss);
        Assert.Equal(safeCss, result);
    }

    [Fact]
    public void ColorContrast_CalculatesCorrectRatios()
    {
        // White on black = 21:1
        var ratio = ColorContrastValidator.CalculateContrastRatio("#FFFFFF", "#000000");
        Assert.True(ratio >= 20.9);

        // Invalid colors throw HttpApiException
        Assert.Throws<HttpApiException>(() => ColorContrastValidator.ValidateHex("blue", "PrimaryColor"));
        Assert.Throws<HttpApiException>(() => ColorContrastValidator.ValidateHex("#12345", "PrimaryColor"));
        Assert.Throws<HttpApiException>(() => ColorContrastValidator.ValidateHex("#GGGGGG", "PrimaryColor"));
    }

    [Fact]
    public void SvgSanitizer_StripsJavaScriptAndEventHandlers()
    {
        var maliciousSvg = @"<svg xmlns=""http://www.w3.org/2000/svg"" onload=""alert(1)""><script>alert('XSS')</script><circle cx=""50"" cy=""50"" r=""40"" /></svg>";
        var cleanBytes = SvgSanitizer.Sanitize(Encoding.UTF8.GetBytes(maliciousSvg));
        var cleanXml = Encoding.UTF8.GetString(cleanBytes);

        Assert.DoesNotContain("<script", cleanXml);
        Assert.DoesNotContain("onload", cleanXml);
        Assert.Contains("<circle", cleanXml);
    }

    [Theory]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><foreignObject><div xmlns=""http://www.w3.org/1999/xhtml"">x</div></foreignObject><circle r=""1""/></svg>", "foreignObject")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:s=""http://www.w3.org/2000/svg""><s:script>alert(1)</s:script><circle r=""1""/></svg>", "script")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><a href=""&#106;avascript:alert(1)""><circle r=""1""/></a></svg>", "href")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink""><a xlink:href=""http://evil.example""><circle r=""1""/></a></svg>", "evil.example")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><a><set attributeName=""href"" to=""x""/><circle r=""1""/></a></svg>", "<set")]
    [InlineData(@"<?xml-stylesheet href=""http://evil.example/x.css""?><svg xmlns=""http://www.w3.org/2000/svg""><circle r=""1""/></svg>", "xml-stylesheet")]
    [InlineData(@"<svg xmlns=""http://www.w3.org/2000/svg""><circle r=""1"" fill=""url(https://evil.example/a)""/></svg>", "evil.example")]
    public void SvgSanitizer_RemovesObfuscatedVectors(string svg, string forbidden)
    {
        var cleanXml = Encoding.UTF8.GetString(SvgSanitizer.Sanitize(Encoding.UTF8.GetBytes(svg)));

        Assert.DoesNotContain(forbidden, cleanXml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("circle", cleanXml);
    }

    [Fact]
    public void SvgSanitizer_KeepsLocalReferences()
    {
        var svg = @"<svg xmlns=""http://www.w3.org/2000/svg""><defs><linearGradient id=""g""/></defs><circle r=""1"" fill=""url(#g)""/><a href=""#g""><circle r=""2""/></a></svg>";
        var cleanXml = Encoding.UTF8.GetString(SvgSanitizer.Sanitize(Encoding.UTF8.GetBytes(svg)));

        Assert.Contains("url(#g)", cleanXml);
        Assert.Contains(@"href=""#g""", cleanXml);
    }

    [Theory]
    [InlineData(@"@\69mport url(x.css);")]
    [InlineData(@"f\6f oter { display: none; }")]
    [InlineData("</style><script>alert(1)</script>")]
    public void CssSanitizer_BlocksEscapedBypasses(string css)
    {
        Assert.Throws<HttpApiException>(() => CssSanitizer.Sanitize(css));
    }
}
