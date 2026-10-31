using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.System.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.System.Http;

[ApiController]
[Route("api/admin/settings")]
public class AdminSettingsController : ControllerBase
{
    private readonly ISystemSettingsService _settingsService;
    private readonly ILogger<AdminSettingsController> _logger;

    public AdminSettingsController(ISystemSettingsService settingsService, ILogger<AdminSettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminSystemRead)]
    public Task<AdminSettingsResponse> Get(CancellationToken cancellationToken)
        => _settingsService.GetSettingsAsync(cancellationToken);

    [HttpPut]
    [RequirePermission(Permissions.AdminSystemWrite)]
    public async Task<AdminSettingsResponse> Update([FromBody] AdminSettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _settingsService.UpdateSettingsAsync(request, cancellationToken);
        _logger.LogInformation("User {ActorId} updated system settings", User.GetUserId());
        return result;
    }

    /// <summary>
    /// Generates a confidential diagnostic archive (troubleshooting.zip) for support purposes.
    /// All sensitive secrets, tokens, and database passwords are automatically redacted.
    /// </summary>
    [HttpGet("troubleshooting")]
    [RequirePermission(Permissions.AdminSystemRead)]
    public async Task<IActionResult> DownloadTroubleshooting(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User {ActorId} requested troubleshooting.zip diagnostic bundle", User.GetUserId());
        var zipBytes = await _settingsService.GenerateTroubleshootingZipAsync(cancellationToken);
        return File(zipBytes, "application/zip", "troubleshooting.zip");
    }
}
