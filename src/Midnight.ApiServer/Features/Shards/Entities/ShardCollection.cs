namespace Midnight.ApiServer.Features.Shards.Entities;

/// <summary>
/// A collection of related shards (e.g. "Minecraft", "Source Engine", "Voice Servers").
/// </summary>
public class ShardCollection
{
    public int Id { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Author { get; set; } = "";

    public ICollection<Shard> Shards { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
