namespace Midnight.ApiServer.Features.Users.Entities;

/// <summary>
/// A login session backed by a rotating refresh token. Only the SHA-256 hash of the
/// current refresh token is stored.
/// </summary>
public class UserSession
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string RefreshTokenHash { get; set; } = "";

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
