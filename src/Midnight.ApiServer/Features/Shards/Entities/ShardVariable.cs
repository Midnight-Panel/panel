namespace Midnight.ApiServer.Features.Shards.Entities;

/// <summary>
/// Konfigurierbare Umgebungsvariable eines Shards mit Validierungsregeln und Benutzerrechten.
/// </summary>
public class ShardVariable
{
    public int Id { get; set; }

    public int ShardId { get; set; }
    public Shard Shard { get; set; } = null!;

    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string EnvVariable { get; set; } = "";
    public string? DefaultValue { get; set; }

    public bool UserViewable { get; set; } = true;
    public bool UserEditable { get; set; } = true;

    /// <summary>Validierungsregeln (z. B. "required|string|max:20" oder "required|numeric|between:1024,65535")</summary>
    public string Rules { get; set; } = "required|string";

    public int SortOrder { get; set; }
}
