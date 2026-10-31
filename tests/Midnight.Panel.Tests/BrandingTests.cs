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
        // Weiß gegen Schwarz = 21:1
        var ratio = ColorContrastValidator.CalculateContrastRatio("#FFFFFF", "#000000");
        Assert.True(ratio >= 20.9);

        // Ungültige Farben werfen HttpApiException
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
}
