using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Extensions.Services;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Extensions;

namespace Midnight.ApiServer.Features.Extensions.Http;

[ApiController]
[Authorize]
[Route("api/admin/extensions")]
public class ExtensionsController : ControllerBase
{
    private readonly IExtensionService _extensionService;

    public ExtensionsController(IExtensionService extensionService)
    {
        _extensionService = extensionService;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminExtensionsRead)]
    public Task<PagedResult<ExtensionSummaryResponse>> List(
        [FromQuery] PagedQuery query,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
        => _extensionService.ListAsync(query, category, cancellationToken);

    [HttpGet("categories")]
    [RequirePermission(Permissions.AdminExtensionsRead)]
    public Task<IReadOnlyList<string>> GetCategories(CancellationToken cancellationToken)
        => _extensionService.GetCategoriesAsync(cancellationToken);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.AdminExtensionsRead)]
    public Task<ExtensionDetailResponse> Get(int id, CancellationToken cancellationToken)
        => _extensionService.GetByIdAsync(id, cancellationToken);

    [HttpPut("{id:int}/toggle")]
    [RequirePermission(Permissions.AdminExtensionsWrite)]
    public Task<ExtensionDetailResponse> Toggle(int id, [FromBody] ExtensionToggleRequest request, CancellationToken cancellationToken)
        => _extensionService.ToggleAsync(id, request.IsEnabled, cancellationToken);

    [HttpPut("{id:int}/config")]
    [RequirePermission(Permissions.AdminExtensionsWrite)]
    public Task<ExtensionDetailResponse> UpdateConfig(int id, [FromBody] ExtensionConfigUpdateRequest request, CancellationToken cancellationToken)
        => _extensionService.UpdateConfigAsync(id, request.ConfigValuesJson, cancellationToken);

    [HttpPost]
    [RequirePermission(Permissions.AdminExtensionsWrite)]
    public Task<ExtensionDetailResponse> Install([FromBody] ExtensionInstallRequest request, CancellationToken cancellationToken)
        => _extensionService.InstallAsync(request, cancellationToken);

    [HttpPost("upload")]
    [RequirePermission(Permissions.AdminExtensionsWrite)]
    public async Task<ExtensionDetailResponse> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return await _extensionService.UploadPackageAsync(stream, file.FileName, cancellationToken);
    }

    [HttpPost("url")]
    [RequirePermission(Permissions.AdminExtensionsWrite)]
    public Task<ExtensionDetailResponse> InstallUrl([FromBody] ExtensionInstallUrlRequest request, CancellationToken cancellationToken)
        => _extensionService.InstallFromUrlAsync(request.Url, cancellationToken);

    [HttpGet("{id:int}/download")]
    [RequirePermission(Permissions.AdminExtensionsRead)]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var (stream, fileName, contentType) = await _extensionService.DownloadPackageAsync(id, cancellationToken);
        return File(stream, contentType, fileName);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminExtensionsWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _extensionService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
