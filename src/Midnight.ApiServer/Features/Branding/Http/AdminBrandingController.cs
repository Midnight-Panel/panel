using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Branding.Models;
using Midnight.ApiServer.Features.Branding.Services;
using Midnight.Shared;

namespace Midnight.ApiServer.Features.Branding.Http;

[ApiController]
[Route("api/admin/branding")]
[RequirePermission(Permissions.AdminSystemWrite)]
public class AdminBrandingController : ControllerBase
{
    private readonly IBrandingService _brandingService;

    public AdminBrandingController(IBrandingService brandingService)
    {
        _brandingService = brandingService;
    }

    [HttpPut]
    public Task<BrandingResponse> Update([FromBody] BrandingUpdateRequest request, CancellationToken cancellationToken)
        => _brandingService.UpdateAsync(request, User.GetUserId(), cancellationToken);

    [HttpPost("logo")]
    [Consumes("multipart/form-data")]
    public async Task<BrandingResponse> UploadLogo([FromQuery] LogoThemeType theme, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return await _brandingService.UploadLogoAsync(theme, stream, file.FileName, User.GetUserId(), cancellationToken);
    }

    [HttpPost("favicon")]
    [Consumes("multipart/form-data")]
    public async Task<BrandingResponse> UploadFavicon(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return await _brandingService.UploadFaviconAsync(stream, file.FileName, User.GetUserId(), cancellationToken);
    }

    [HttpPost("reset")]
    public Task<BrandingResponse> Reset(CancellationToken cancellationToken)
        => _brandingService.ResetToDefaultsAsync(User.GetUserId(), cancellationToken);
}
