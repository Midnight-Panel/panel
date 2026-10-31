using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Midnight.ApiServer.Features.Branding.Models;

namespace Midnight.ApiServer.Features.Branding.Services;

public interface IBrandingService
{
    Task<BrandingResponse> GetAsync(CancellationToken cancellationToken);
    Task<BrandingResponse> UpdateAsync(BrandingUpdateRequest request, int actorUserId, CancellationToken cancellationToken);
    Task<BrandingResponse> UploadLogoAsync(LogoThemeType theme, Stream content, string originalName, int actorUserId, CancellationToken cancellationToken);
    Task<BrandingResponse> UploadFaviconAsync(Stream content, string originalName, int actorUserId, CancellationToken cancellationToken);
    Task<BrandingResponse> ResetToDefaultsAsync(int actorUserId, CancellationToken cancellationToken);
    Task<(string Path, string ContentType)?> GetFileAsync(string fileName, CancellationToken cancellationToken);
}
