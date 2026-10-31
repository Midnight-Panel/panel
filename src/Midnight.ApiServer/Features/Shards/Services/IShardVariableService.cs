using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Services;

public interface IShardVariableService
{
    Task<List<ShardVariableResponse>> ListByShardAsync(int shardId, CancellationToken cancellationToken);
    Task<ShardVariable> GetAsync(int shardId, int id, CancellationToken cancellationToken);
    Task<ShardVariableResponse> CreateAsync(int shardId, ShardVariableRequest request, CancellationToken cancellationToken);
    Task<ShardVariableResponse> UpdateAsync(int shardId, int id, ShardVariableRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int shardId, int id, CancellationToken cancellationToken);
}
