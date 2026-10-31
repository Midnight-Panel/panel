using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Shards.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Http;

[ApiController]
[Route("api/admin/shards/{shardId:int}/variables")]
public class AdminShardVariablesController : ControllerBase
{
    private readonly IShardVariableService _variableService;
    private readonly ILogger<AdminShardVariablesController> _logger;

    public AdminShardVariablesController(IShardVariableService variableService, ILogger<AdminShardVariablesController> logger)
    {
        _variableService = variableService;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminShardsRead)]
    public Task<List<ShardVariableResponse>> List(int shardId, CancellationToken cancellationToken)
        => _variableService.ListByShardAsync(shardId, cancellationToken);

    [HttpPost]
    [RequirePermission(Permissions.AdminShardsWrite)]
    public async Task<ShardVariableResponse> Create(int shardId, [FromBody] ShardVariableRequest request, CancellationToken cancellationToken)
    {
        var variable = await _variableService.CreateAsync(shardId, request, cancellationToken);
        _logger.LogInformation("User {ActorId} created variable {VarId} ({Env}) for shard {ShardId}",
            User.GetUserId(), variable.Id, variable.EnvVariable, shardId);
        return variable;
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AdminShardsWrite)]
    public async Task<ShardVariableResponse> Update(int shardId, int id, [FromBody] ShardVariableRequest request, CancellationToken cancellationToken)
    {
        var variable = await _variableService.UpdateAsync(shardId, id, request, cancellationToken);
        _logger.LogInformation("User {ActorId} updated variable {VarId} for shard {ShardId}",
            User.GetUserId(), id, shardId);
        return variable;
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminShardsWrite)]
    public async Task<IActionResult> Delete(int shardId, int id, CancellationToken cancellationToken)
    {
        await _variableService.DeleteAsync(shardId, id, cancellationToken);
        _logger.LogInformation("User {ActorId} deleted variable {VarId} for shard {ShardId}",
            User.GetUserId(), id, shardId);
        return NoContent();
    }
}
