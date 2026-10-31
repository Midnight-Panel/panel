using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Services;

public interface IShardCollectionService
{
    Task<PagedResult<ShardCollectionResponse>> ListAsync(PagedQuery query, CancellationToken cancellationToken);
    Task<List<ShardCollectionResponse>> ListAllAsync(CancellationToken cancellationToken);
    Task<ShardCollection> GetAsync(int id, CancellationToken cancellationToken);
    Task<ShardCollectionResponse> GetResponseAsync(int id, CancellationToken cancellationToken);
    Task<ShardCollectionResponse> CreateAsync(ShardCollectionRequest request, CancellationToken cancellationToken);
    Task<ShardCollectionResponse> UpdateAsync(int id, ShardCollectionRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
