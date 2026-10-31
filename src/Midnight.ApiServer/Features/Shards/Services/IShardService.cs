using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Services;

public interface IShardService
{
    Task<PagedResult<ShardSummaryResponse>> ListAsync(PagedQuery query, int? collectionId, CancellationToken cancellationToken);
    Task<List<ShardSummaryResponse>> ListUserSelectableAsync(int? collectionId, CancellationToken cancellationToken);
    Task<Shard> GetAsync(int id, CancellationToken cancellationToken);
    Task<ShardDetailResponse> GetDetailAsync(int id, CancellationToken cancellationToken);
    Task<ShardDetailResponse> CreateAsync(ShardRequest request, CancellationToken cancellationToken);
    Task<ShardDetailResponse> UpdateAsync(int id, ShardRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
    Task<ValidateVariablesResponse> ValidateVariablesAsync(int shardId, ValidateVariablesRequest request, CancellationToken cancellationToken);
}
