namespace Midnight.ApiServer.Features.Shards.Entities;

/// <summary>
/// Eine Gruppe bzw. Sammlung thematisch zusammengehörender Shards (z. B. "Minecraft", "Source Engine", "Voice Server").
/// Entspricht dem Nest-Konzept, heißt im Midnightpanel Shard Collection.
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
