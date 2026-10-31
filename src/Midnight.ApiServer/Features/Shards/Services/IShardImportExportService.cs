using Microsoft.AspNetCore.Http;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Services;

public interface IShardImportExportService
{
    Task<ShardExportPackage> ExportAsync(int shardId, CancellationToken cancellationToken);
    Task<ShardImportResult> ImportAsync(ShardImportRequest request, CancellationToken cancellationToken);
    Task<ShardBatchImportResult> ImportBatchAsync(ShardBatchImportRequest request, CancellationToken cancellationToken);
    Task<ShardBatchImportResult> ImportFilesAsync(IFormFileCollection files, int? targetCollectionId, CancellationToken cancellationToken);
}
