using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Services;

public class ShardCollectionService : IShardCollectionService
{
    private readonly DataContext _context;
    private readonly TimeProvider _timeProvider;

    public ShardCollectionService(DataContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<PagedResult<ShardCollectionResponse>> ListAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        var collections = _context.ShardCollections.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            collections = collections.Where(x => x.Name.ToLower().Contains(search) || (x.Description != null && x.Description.ToLower().Contains(search)));
        }

        collections = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", false) => collections.OrderBy(x => x.Name),
            ("name", true) => collections.OrderByDescending(x => x.Name),
            ("author", false) => collections.OrderBy(x => x.Author),
            ("author", true) => collections.OrderByDescending(x => x.Author),
            (_, true) => collections.OrderByDescending(x => x.Id),
            _ => collections.OrderBy(x => x.Id)
        };

        var total = await collections.CountAsync(cancellationToken);

        var items = await collections
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(x => new ShardCollectionResponse
            {
                Id = x.Id,
                Uuid = x.Uuid,
                Name = x.Name,
                Description = x.Description,
                Author = x.Author,
                ShardCount = x.Shards.Count,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ShardCollectionResponse>
        {
            Items = items,
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            TotalCount = total
        };
    }

    public async Task<List<ShardCollectionResponse>> ListAllAsync(CancellationToken cancellationToken)
    {
        return await _context.ShardCollections
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ShardCollectionResponse
            {
                Id = x.Id,
                Uuid = x.Uuid,
                Name = x.Name,
                Description = x.Description,
                Author = x.Author,
                ShardCount = x.Shards.Count,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ShardCollection> GetAsync(int id, CancellationToken cancellationToken)
        => await _context.ShardCollections.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
           ?? throw HttpApiException.NotFound("Shard collection not found");

    public async Task<ShardCollectionResponse> GetResponseAsync(int id, CancellationToken cancellationToken)
    {
        var collection = await GetAsync(id, cancellationToken);
        var shardCount = await _context.Shards.CountAsync(x => x.CollectionId == id, cancellationToken);

        return new ShardCollectionResponse
        {
            Id = collection.Id,
            Uuid = collection.Uuid,
            Name = collection.Name,
            Description = collection.Description,
            Author = collection.Author,
            ShardCount = shardCount,
            CreatedAt = collection.CreatedAt,
            UpdatedAt = collection.UpdatedAt
        };
    }

    public async Task<ShardCollectionResponse> CreateAsync(ShardCollectionRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw HttpApiException.BadRequest("Shard collection name cannot be empty.");

        await EnsureUniqueNameAsync(name, null, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var collection = new ShardCollection
        {
            Uuid = Guid.NewGuid(),
            Name = name,
            Description = request.Description?.Trim(),
            Author = string.IsNullOrWhiteSpace(request.Author) ? "Midnightpanel" : request.Author.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ShardCollections.Add(collection);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(collection.Id, cancellationToken);
    }

    public async Task<ShardCollectionResponse> UpdateAsync(int id, ShardCollectionRequest request, CancellationToken cancellationToken)
    {
        var collection = await GetAsync(id, cancellationToken);

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw HttpApiException.BadRequest("Shard collection name cannot be empty.");

        await EnsureUniqueNameAsync(name, id, cancellationToken);

        collection.Name = name;
        collection.Description = request.Description?.Trim();
        collection.Author = string.IsNullOrWhiteSpace(request.Author) ? "Midnightpanel" : request.Author.Trim();
        collection.UpdatedAt = _timeProvider.GetUtcNow();

        await _context.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var collection = await GetAsync(id, cancellationToken);

        var shardCount = await _context.Shards.CountAsync(x => x.CollectionId == id, cancellationToken);
        if (shardCount > 0)
            throw HttpApiException.Conflict(
                "Cannot delete collection",
                $"This collection still contains {shardCount} shard(s). Delete or move them first.");

        _context.ShardCollections.Remove(collection);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUniqueNameAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        var lower = name.ToLower();
        if (await _context.ShardCollections.AnyAsync(x => x.Id != exceptId && x.Name.ToLower() == lower, cancellationToken))
            throw HttpApiException.Conflict("A shard collection with this name already exists.");
    }
}
