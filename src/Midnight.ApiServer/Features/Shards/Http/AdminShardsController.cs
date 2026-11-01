using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Shards.Services;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Http;

[ApiController]
[Route("api/admin/shards")]
public class AdminShardsController : ControllerBase
{
    private readonly IShardService _shardService;
    private readonly IShardImportExportService _importExportService;
    private readonly ILogger<AdminShardsController> _logger;

    public AdminShardsController(
        IShardService shardService,
        IShardImportExportService importExportService,
        ILogger<AdminShardsController> logger)
    {
        _shardService = shardService;
        _importExportService = importExportService;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminShardsRead)]
    public Task<PagedResult<ShardSummaryResponse>> List(
        [FromQuery] PagedQuery query,
        [FromQuery] int? collectionId,
        CancellationToken cancellationToken)
        => _shardService.ListAsync(query, collectionId, cancellationToken);

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.AdminShardsRead)]
    public Task<ShardDetailResponse> Get(int id, CancellationToken cancellationToken)
        => _shardService.GetDetailAsync(id, cancellationToken);

    [HttpPost]
    [RequirePermission(Permissions.AdminShardsWrite)]
    public async Task<ShardDetailResponse> Create([FromBody] ShardRequest request, CancellationToken cancellationToken)
    {
        var shard = await _shardService.CreateAsync(request, cancellationToken);
        _logger.LogInformation("User {ActorId} created shard {ShardId} ({Name})", User.GetUserId(), shard.Id, shard.Name);
        return shard;
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AdminShardsWrite)]
    public async Task<ShardDetailResponse> Update(int id, [FromBody] ShardRequest request, CancellationToken cancellationToken)
    {
        var shard = await _shardService.UpdateAsync(id, request, cancellationToken);
        _logger.LogInformation("User {ActorId} updated shard {ShardId}", User.GetUserId(), id);
        return shard;
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminShardsWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _shardService.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("User {ActorId} deleted shard {ShardId}", User.GetUserId(), id);
        return NoContent();
    }

    /// <summary>Exports shard template as JSON package (version midnight_shard_v1)</summary>
    [HttpGet("{id:int}/export")]
    [RequirePermission(Permissions.AdminShardsRead)]
    public async Task<IActionResult> Export(int id, CancellationToken cancellationToken)
    {
        var package = await _importExportService.ExportAsync(id, cancellationToken);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(package, new JsonSerializerOptions { WriteIndented = true });
        var filename = $"shard-{package.Shard.Name.ToLowerInvariant().Replace(' ', '-')}-{package.Shard.Version}.json";

        return File(jsonBytes, "application/json", filename);
    }

    /// <summary>Imports a shard template (supports native Midnight format and legacy PTDL format)</summary>
    [HttpPost("import")]
    [RequirePermission(Permissions.AdminShardsWrite)]
    public async Task<ShardImportResult> Import([FromBody] ShardImportRequest request, CancellationToken cancellationToken)
    {
        var result = await _importExportService.ImportAsync(request, cancellationToken);
        _logger.LogInformation("User {ActorId} imported shard {ShardId} ({Name}) via {Format}",
            User.GetUserId(), result.ShardId, result.ShardName, result.DetectedFormat);
        return result;
    }

    /// <summary>Batch imports multiple shard templates simultaneously</summary>
    [HttpPost("import-batch")]
    [RequirePermission(Permissions.AdminShardsWrite)]
    [RequestSizeLimit(ShardImportExportService.MaxUploadSize)]
    public async Task<ShardBatchImportResult> ImportBatch([FromBody] ShardBatchImportRequest request, CancellationToken cancellationToken)
    {
        var result = await _importExportService.ImportBatchAsync(request, cancellationToken);
        _logger.LogInformation("User {ActorId} batch-imported {Succeeded}/{Total} shards",
            User.GetUserId(), result.SucceededCount, result.TotalCount);
        return result;
    }

    /// <summary>Uploads and imports multiple shard JSON files or ZIP archive simultaneously</summary>
    [HttpPost("import-files")]
    [RequirePermission(Permissions.AdminShardsWrite)]
    [RequestSizeLimit(ShardImportExportService.MaxUploadSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = ShardImportExportService.MaxUploadSize)]
    public async Task<ShardBatchImportResult> ImportFiles(
        [FromForm] IFormFileCollection files,
        [FromQuery] int? targetCollectionId,
        CancellationToken cancellationToken)
    {
        var result = await _importExportService.ImportFilesAsync(files, targetCollectionId, cancellationToken);
        _logger.LogInformation("User {ActorId} imported {Succeeded}/{Total} shards from uploaded files",
            User.GetUserId(), result.SucceededCount, result.TotalCount);
        return result;
    }
}
