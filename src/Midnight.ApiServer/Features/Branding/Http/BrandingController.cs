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
    /// Öffentlicher Endpunkt: Lädt die aktuellen Branding-Einstellungen für Login-Page und Panel.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public Task<BrandingResponse> Get(CancellationToken cancellationToken)
        => _brandingService.GetAsync(cancellationToken);

    /// <summary>
    /// Öffentlicher Dateiendpunkt für Logos und Favicon mit 30-Tage Cache.
    /// </summary>
    [HttpGet("files/{fileName}")]
    [ResponseCache(Duration = 2592000, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetFile(string fileName, CancellationToken cancellationToken)
    {
        var file = await _brandingService.GetFileAsync(fileName, cancellationToken);
        if (file == null)
            return NotFound();

        return PhysicalFile(file.Value.Path, file.Value.ContentType, enableRangeProcessing: false);
    }
}
