namespace Midnight.ApiServer.Features.Users.Entities;

/// <summary>
/// Api key of the format "mdp_{prefix}_{secret}". The prefix is stored in plain text for lookup,
/// the secret only as SHA-256 hash.
/// </summary>
public class ApiKey
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = "";
    public string Prefix { get; set; } = "";
    public string KeyHash { get; set; } = "";

    public List<string> Scopes { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
