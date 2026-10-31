using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Features.Shards.Services;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Http;

[ApiController]
[Authorize]
[Route("api/shards")]
public class ShardsController : ControllerBase
{
    private readonly IShardService _shardService;

    public ShardsController(IShardService shardService)
    {
        _shardService = shardService;
    }

    /// <summary>List of available shards for server creation</summary>
    [HttpGet]
    public Task<List<ShardSummaryResponse>> List([FromQuery] int? collectionId, CancellationToken cancellationToken)
        => _shardService.ListUserSelectableAsync(collectionId, cancellationToken);

    /// <summary>Details of a shard for server configuration</summary>
    [HttpGet("{id:int}")]
    public async Task<ShardDetailResponse> Get(int id, CancellationToken cancellationToken)
    {
        var shard = await _shardService.GetDetailAsync(id, cancellationToken);

        // Filter out variables that are not visible to regular users
        shard.Variables = shard.Variables.Where(v => v.UserViewable).ToList();
        return shard;
    }

    /// <summary>Validates user-provided variable values and generates startup command preview</summary>
    [HttpPost("{id:int}/validate-variables")]
    public Task<ValidateVariablesResponse> ValidateVariables(int id, [FromBody] ValidateVariablesRequest request, CancellationToken cancellationToken)
        => _shardService.ValidateVariablesAsync(id, request, cancellationToken);
}
