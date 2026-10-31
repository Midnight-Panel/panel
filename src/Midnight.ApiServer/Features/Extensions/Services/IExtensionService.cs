using Midnight.Shared.Http;
using Midnight.Shared.Http.Extensions;

namespace Midnight.ApiServer.Features.Extensions.Services;

public interface IExtensionService
{
    Task<PagedResult<ExtensionSummaryResponse>> ListAsync(PagedQuery query, string? category = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<ExtensionDetailResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ExtensionDetailResponse> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
    Task<ExtensionDetailResponse> ToggleAsync(int id, bool isEnabled, CancellationToken cancellationToken = default);
    Task<ExtensionDetailResponse> UpdateConfigAsync(int id, string configValuesJson, CancellationToken cancellationToken = default);
    Task<ExtensionDetailResponse> InstallAsync(ExtensionInstallRequest request, CancellationToken cancellationToken = default);
    Task<ExtensionDetailResponse> UploadPackageAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    Task<ExtensionDetailResponse> InstallFromUrlAsync(string url, CancellationToken cancellationToken = default);
    Task<(Stream fileStream, string fileName, string contentType)> DownloadPackageAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
