namespace Midnight.Shared.Http.Shards;

/// <summary>Response model for a shard collection</summary>
public class ShardCollectionResponse
{
    public int Id { get; set; }
    public Guid Uuid { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Author { get; set; } = "";
    public int ShardCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Request model to create or update a shard collection</summary>
public class ShardCollectionRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Author { get; set; } = "";
}
