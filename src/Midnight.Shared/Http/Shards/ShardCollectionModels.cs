namespace Midnight.Shared.Http.Shards;

/// <summary>Antwortmodell für eine Shard-Sammlung</summary>
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

/// <summary>Anfragemodell zum Erstellen oder Bearbeiten einer Shard-Sammlung</summary>
public class ShardCollectionRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Author { get; set; } = "";
}
