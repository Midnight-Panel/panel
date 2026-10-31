using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Features.Branding.Models;
using Midnight.ApiServer.Features.Branding.Services;

namespace Midnight.ApiServer.Features.Branding.Http;

[ApiController]
[Route("api/branding")]
public class BrandingController : ControllerBase
{
    private readonly IBrandingService _brandingService;

    public BrandingController(IBrandingService brandingService)
    {
        _brandingService = brandingService;
    }

    /// <summary>
    /// Public endpoint: returns the current branding settings for the login page and the panel.
    /// </summary>
    [HttpGet]
    // Always revalidate, otherwise changes in the customizer show up only minutes later
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<BrandingResponse> Get(CancellationToken cancellationToken)
        => _brandingService.GetAsync(cancellationToken);

    /// <summary>
    /// Public file endpoint for logos and favicon with a 30-day cache.
    /// </summary>
    [HttpGet("files/{fileName}")]
    [ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetFile(string fileName, CancellationToken cancellationToken)
    {
        var file = await _brandingService.GetFileAsync(fileName, cancellationToken);
        if (file == null)
            return NotFound();

        // Defense in depth for SVGs opened directly: no scripts, no external resources
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src data:; style-src 'unsafe-inline'; sandbox";

        return PhysicalFile(file.Value.Path, file.Value.ContentType, enableRangeProcessing: false);
    }
}
