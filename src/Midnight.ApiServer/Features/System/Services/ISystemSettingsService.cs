using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.System.Services;

public interface ISystemSettingsService
{
    Task<AdminSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken);
    Task<AdminSettingsResponse> UpdateSettingsAsync(AdminSettingsUpdateRequest request, CancellationToken cancellationToken);
    Task<byte[]> GenerateTroubleshootingZipAsync(CancellationToken cancellationToken);
}
