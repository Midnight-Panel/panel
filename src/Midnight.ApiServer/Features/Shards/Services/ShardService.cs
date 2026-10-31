using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Services;

public class ShardService : IShardService
{
    private readonly DataContext _context;
    private readonly TimeProvider _timeProvider;

    public ShardService(DataContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<PagedResult<ShardSummaryResponse>> ListAsync(PagedQuery query, int? collectionId, CancellationToken cancellationToken)
    {
        var shards = _context.Shards.AsNoTracking().Include(x => x.Collection).AsQueryable();

        if (collectionId.HasValue)
            shards = shards.Where(x => x.CollectionId == collectionId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            shards = shards.Where(x => x.Name.ToLower().Contains(search) ||
                                       (x.Description != null && x.Description.ToLower().Contains(search)) ||
                                       x.Collection.Name.ToLower().Contains(search));
        }

        shards = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", false) => shards.OrderBy(x => x.Name),
            ("name", true) => shards.OrderByDescending(x => x.Name),
            ("collection", false) => shards.OrderBy(x => x.Collection.Name).ThenBy(x => x.Name),
            ("collection", true) => shards.OrderByDescending(x => x.Collection.Name).ThenByDescending(x => x.Name),
            ("author", false) => shards.OrderBy(x => x.Author),
            ("author", true) => shards.OrderByDescending(x => x.Author),
            (_, true) => shards.OrderByDescending(x => x.Id),
            _ => shards.OrderBy(x => x.Id)
        };

        var total = await shards.CountAsync(cancellationToken);

        var items = await shards
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(x => new ShardSummaryResponse
            {
                Id = x.Id,
                Uuid = x.Uuid,
                CollectionId = x.CollectionId,
                CollectionName = x.Collection.Name,
                Name = x.Name,
                Description = x.Description,
                Author = x.Author,
                Version = x.Version,
                DefaultDockerImage = x.DefaultDockerImage,
                VariableCount = x.Variables.Count,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ShardSummaryResponse>
        {
            Items = items,
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            TotalCount = total
        };
    }

    public async Task<List<ShardSummaryResponse>> ListUserSelectableAsync(int? collectionId, CancellationToken cancellationToken)
    {
        var shards = _context.Shards.AsNoTracking().Include(x => x.Collection).AsQueryable();

        if (collectionId.HasValue)
            shards = shards.Where(x => x.CollectionId == collectionId.Value);

        return await shards
            .OrderBy(x => x.Collection.Name)
            .ThenBy(x => x.Name)
            .Select(x => new ShardSummaryResponse
            {
                Id = x.Id,
                Uuid = x.Uuid,
                CollectionId = x.CollectionId,
                CollectionName = x.Collection.Name,
                Name = x.Name,
                Description = x.Description,
                Author = x.Author,
                Version = x.Version,
                DefaultDockerImage = x.DefaultDockerImage,
                VariableCount = x.Variables.Count(v => v.UserViewable),
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Shard> GetAsync(int id, CancellationToken cancellationToken)
        => await _context.Shards
               .Include(x => x.Collection)
               .Include(x => x.Variables)
               .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
           ?? throw HttpApiException.NotFound("Shard not found");

    public async Task<ShardDetailResponse> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        var shard = await GetAsync(id, cancellationToken);
        return ToDetailResponse(shard);
    }

    public async Task<ShardDetailResponse> CreateAsync(ShardRequest request, CancellationToken cancellationToken)
    {
        var collection = await _context.ShardCollections.FirstOrDefaultAsync(x => x.Id == request.CollectionId, cancellationToken)
                         ?? throw HttpApiException.BadRequest("The specified shard collection does not exist.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw HttpApiException.BadRequest("Shard name cannot be empty.");

        await EnsureUniqueNameAsync(request.CollectionId, name, null, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var shard = new Shard
        {
            Uuid = Guid.NewGuid(),
            CollectionId = request.CollectionId,
            Name = name,
            Description = request.Description?.Trim(),
            Author = string.IsNullOrWhiteSpace(request.Author) ? "Midnightpanel" : request.Author.Trim(),
            Version = string.IsNullOrWhiteSpace(request.Version) ? "1.0.0" : request.Version.Trim(),
            DefaultDockerImage = request.DefaultDockerImage?.Trim() ?? "",
            StartupCommand = request.StartupCommand?.Trim() ?? "",
            StopCommand = request.StopCommand?.Trim(),
            StopSignal = string.IsNullOrWhiteSpace(request.StopSignal) ? "SIGTERM" : request.StopSignal.Trim(),
            StartupDone = request.StartupDone?.Trim() ?? "",
            InstallScript = request.InstallScript,
            InstallImage = string.IsNullOrWhiteSpace(request.InstallImage) ? "ghcr.io/midnightpanel/installers:alpine" : request.InstallImage.Trim(),
            InstallInterpreter = string.IsNullOrWhiteSpace(request.InstallInterpreter) ? "/bin/bash" : request.InstallInterpreter.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        shard.SetDockerImages(request.DockerImages);
        shard.SetConfigFiles(request.ConfigFiles);
        shard.SetFeatures(request.Features);

        // Fallback for DefaultDockerImage if empty
        if (string.IsNullOrWhiteSpace(shard.DefaultDockerImage) && request.DockerImages.Count > 0)
        {
            shard.DefaultDockerImage = request.DockerImages.Values.First();
        }

        _context.Shards.Add(shard);
        await _context.SaveChangesAsync(cancellationToken);

        shard.Collection = collection;
        return ToDetailResponse(shard);
    }

    public async Task<ShardDetailResponse> UpdateAsync(int id, ShardRequest request, CancellationToken cancellationToken)
    {
        var shard = await GetAsync(id, cancellationToken);

        if (shard.CollectionId != request.CollectionId)
        {
            var exists = await _context.ShardCollections.AnyAsync(x => x.Id == request.CollectionId, cancellationToken);
            if (!exists)
                throw HttpApiException.BadRequest("The specified shard collection does not exist.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw HttpApiException.BadRequest("Shard name cannot be empty.");

        await EnsureUniqueNameAsync(request.CollectionId, name, id, cancellationToken);

        // Validate placeholders in startup command against defined shard variables
        var definedVars = shard.Variables.Select(v => v.EnvVariable).ToList();
        var placeholderErrors = StartupCommandResolver.ValidatePlaceholders(request.StartupCommand ?? "", definedVars);
        if (placeholderErrors.Count > 0)
        {
            throw HttpApiException.BadRequest("Invalid placeholders in startup command", string.Join(" ", placeholderErrors));
        }

        shard.CollectionId = request.CollectionId;
        shard.Name = name;
        shard.Description = request.Description?.Trim();
        shard.Author = string.IsNullOrWhiteSpace(request.Author) ? "Midnightpanel" : request.Author.Trim();
        shard.Version = string.IsNullOrWhiteSpace(request.Version) ? "1.0.0" : request.Version.Trim();
        shard.DefaultDockerImage = request.DefaultDockerImage?.Trim() ?? "";
        shard.StartupCommand = request.StartupCommand?.Trim() ?? "";
        shard.StopCommand = request.StopCommand?.Trim();
        shard.StopSignal = string.IsNullOrWhiteSpace(request.StopSignal) ? "SIGTERM" : request.StopSignal.Trim();
        shard.StartupDone = request.StartupDone?.Trim() ?? "";
        shard.InstallScript = request.InstallScript;
        shard.InstallImage = string.IsNullOrWhiteSpace(request.InstallImage) ? "ghcr.io/midnightpanel/installers:alpine" : request.InstallImage.Trim();
        shard.InstallInterpreter = string.IsNullOrWhiteSpace(request.InstallInterpreter) ? "/bin/bash" : request.InstallInterpreter.Trim();
        shard.UpdatedAt = _timeProvider.GetUtcNow();

        shard.SetDockerImages(request.DockerImages);
        shard.SetConfigFiles(request.ConfigFiles);
        shard.SetFeatures(request.Features);

        if (string.IsNullOrWhiteSpace(shard.DefaultDockerImage) && request.DockerImages.Count > 0)
        {
            shard.DefaultDockerImage = request.DockerImages.Values.First();
        }

        await _context.SaveChangesAsync(cancellationToken);

        return ToDetailResponse(shard);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var shard = await GetAsync(id, cancellationToken);

        _context.Shards.Remove(shard);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ValidateVariablesResponse> ValidateVariablesAsync(int shardId, ValidateVariablesRequest request, CancellationToken cancellationToken)
    {
        var shard = await GetAsync(shardId, cancellationToken);
        var response = new ValidateVariablesResponse();

        foreach (var variable in shard.Variables.OrderBy(x => x.SortOrder))
        {
            // Users can only change editable variables, everything else keeps its default value
            string? userVal = null;
            if (variable.UserEditable)
                request.Values?.TryGetValue(variable.EnvVariable, out userVal);

            var valToValidate = string.IsNullOrEmpty(userVal) ? variable.DefaultValue : userVal;

            var (isValid, error) = ShardVariableValidator.ValidateValue(valToValidate, variable.Rules, variable.Name);
            if (!isValid && error != null)
            {
                response.Errors[variable.EnvVariable] = error;
            }

            // Values of hidden variables are not returned to users
            if (variable.UserViewable)
                response.ResolvedEnvironment[variable.EnvVariable] = valToValidate ?? "";
        }

        if (response.IsValid)
        {
            // Placeholders of hidden variables stay unresolved in the preview
            response.ResolvedStartupCommand = StartupCommandResolver.ResolveStartupCommand(
                shard.StartupCommand,
                response.ResolvedEnvironment);
        }

        return response;
    }

    private async Task EnsureUniqueNameAsync(int collectionId, string name, int? exceptId, CancellationToken cancellationToken)
    {
        var lower = name.ToLower();
        if (await _context.Shards.AnyAsync(x => x.CollectionId == collectionId && x.Id != exceptId && x.Name.ToLower() == lower, cancellationToken))
            throw HttpApiException.Conflict("A shard with this name already exists in this collection.");
    }

    private static ShardDetailResponse ToDetailResponse(Shard shard) => new()
    {
        Id = shard.Id,
        Uuid = shard.Uuid,
        CollectionId = shard.CollectionId,
        CollectionName = shard.Collection?.Name ?? "",
        Name = shard.Name,
        Description = shard.Description,
        Author = shard.Author,
        Version = shard.Version,
        DockerImages = shard.GetDockerImages(),
        DefaultDockerImage = shard.DefaultDockerImage,
        StartupCommand = shard.StartupCommand,
        StopCommand = shard.StopCommand,
        StopSignal = shard.StopSignal,
        ConfigFiles = shard.GetConfigFiles(),
        StartupDone = shard.StartupDone,
        InstallScript = shard.InstallScript,
        InstallImage = shard.InstallImage,
        InstallInterpreter = shard.InstallInterpreter,
        Features = shard.GetFeatures(),
        Variables = shard.Variables?.OrderBy(v => v.SortOrder).Select(v => new ShardVariableResponse
        {
            Id = v.Id,
            ShardId = v.ShardId,
            Name = v.Name,
            Description = v.Description,
            EnvVariable = v.EnvVariable,
            DefaultValue = v.DefaultValue,
            UserViewable = v.UserViewable,
            UserEditable = v.UserEditable,
            Rules = v.Rules,
            SortOrder = v.SortOrder
        }).ToList() ?? [],
        CreatedAt = shard.CreatedAt,
        UpdatedAt = shard.UpdatedAt
    };
}
