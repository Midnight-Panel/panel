using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Services;

public interface IShardImportExportService
{
    Task<ShardExportPackage> ExportAsync(int shardId, CancellationToken cancellationToken);
    Task<ShardImportResult> ImportAsync(ShardImportRequest request, CancellationToken cancellationToken);
}
