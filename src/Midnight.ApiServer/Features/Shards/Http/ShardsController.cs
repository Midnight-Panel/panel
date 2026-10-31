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

    /// <summary>Liste der verfügbaren Shards für die Server-Erstellung</summary>
    [HttpGet]
    public Task<List<ShardSummaryResponse>> List([FromQuery] int? collectionId, CancellationToken cancellationToken)
        => _shardService.ListUserSelectableAsync(collectionId, cancellationToken);

    /// <summary>Details eines Shards für die Server-Konfiguration</summary>
    [HttpGet("{id:int}")]
    public async Task<ShardDetailResponse> Get(int id, CancellationToken cancellationToken)
    {
        var shard = await _shardService.GetDetailAsync(id, cancellationToken);

        // Nicht für reguläre Benutzer sichtbare Variablen filtern
        shard.Variables = shard.Variables.Where(v => v.UserViewable).ToList();
        return shard;
    }

    /// <summary>Validiert vom Benutzer eingegebene Variablenwerte und generiert eine Vorschau</summary>
    [HttpPost("{id:int}/validate-variables")]
    public Task<ValidateVariablesResponse> ValidateVariables(int id, [FromBody] ValidateVariablesRequest request, CancellationToken cancellationToken)
        => _shardService.ValidateVariablesAsync(id, request, cancellationToken);
}
