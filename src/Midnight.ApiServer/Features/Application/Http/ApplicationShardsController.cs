using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Shards.Services;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Application;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Application.Http;

/// <summary>Shard collections and shards, read only</summary>
[Route(RoutePrefix)]
public class ApplicationShardsController : ApplicationControllerBase
{
    private readonly IShardCollectionService _collections;
    private readonly IShardService _shards;

    public ApplicationShardsController(IShardCollectionService collections, IShardService shards)
    {
        _collections = collections;
        _shards = shards;
    }

    [HttpGet("shard-collections")]
    [ApplicationAccess(ApplicationResource.Shards, ApplicationAccess.Read)]
    public Task<PagedResult<ShardCollectionResponse>> Collections([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => _collections.ListAsync(query, cancellationToken);

    [HttpGet("shards")]
    [ApplicationAccess(ApplicationResource.Shards, ApplicationAccess.Read)]
    public Task<PagedResult<ShardSummaryResponse>> List([FromQuery] PagedQuery query, [FromQuery] int? collectionId, CancellationToken cancellationToken)
        => _shards.ListAsync(query, collectionId, cancellationToken);

    /// <summary>With variables, docker images and the install script</summary>
    [HttpGet("shards/{id:int}")]
    [ApplicationAccess(ApplicationResource.Shards, ApplicationAccess.Read)]
    public Task<ShardDetailResponse> Get(int id, CancellationToken cancellationToken)
        => _shards.GetDetailAsync(id, cancellationToken);
}
