using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Shards.Services;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Http;

[ApiController]
[Route("api/admin/shards/collections")]
public class AdminShardCollectionsController : ControllerBase
{
    private readonly IShardCollectionService _collectionService;
    private readonly ILogger<AdminShardCollectionsController> _logger;

    public AdminShardCollectionsController(IShardCollectionService collectionService, ILogger<AdminShardCollectionsController> logger)
    {
        _collectionService = collectionService;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminShardsRead)]
    public Task<PagedResult<ShardCollectionResponse>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => _collectionService.ListAsync(query, cancellationToken);

    [HttpGet("all")]
    [RequirePermission(Permissions.AdminShardsRead)]
    public Task<List<ShardCollectionResponse>> ListAll(CancellationToken cancellationToken)
        => _collectionService.ListAllAsync(cancellationToken);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.AdminShardsRead)]
    public Task<ShardCollectionResponse> Get(int id, CancellationToken cancellationToken)
        => _collectionService.GetResponseAsync(id, cancellationToken);

    [HttpPost]
    [RequirePermission(Permissions.AdminShardsWrite)]
    public async Task<ShardCollectionResponse> Create([FromBody] ShardCollectionRequest request, CancellationToken cancellationToken)
    {
        var collection = await _collectionService.CreateAsync(request, cancellationToken);
        _logger.LogInformation("User {ActorId} created shard collection {CollectionId} ({Name})", User.GetUserId(), collection.Id, collection.Name);
        return collection;
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AdminShardsWrite)]
    public async Task<ShardCollectionResponse> Update(int id, [FromBody] ShardCollectionRequest request, CancellationToken cancellationToken)
    {
        var collection = await _collectionService.UpdateAsync(id, request, cancellationToken);
        _logger.LogInformation("User {ActorId} updated shard collection {CollectionId}", User.GetUserId(), id);
        return collection;
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminShardsWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _collectionService.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("User {ActorId} deleted shard collection {CollectionId}", User.GetUserId(), id);
        return NoContent();
    }
}
