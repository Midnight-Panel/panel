namespace Midnight.ApiServer.Features.Users.Entities;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";

    public string? AvatarFileName { get; set; }

    /// <summary>Data protection encrypted TOTP secret, only set once 2FA is enabled</summary>
    public string? TotpSecret { get; set; }

    /// <summary>Secret generated during 2FA setup, moved to TotpSecret after the first valid code</summary>
    public string? PendingTotpSecret { get; set; }

    public bool TotpEnabled { get; set; }

    /// <summary>Last accepted TOTP time step, used to reject replayed codes</summary>
    public long TotpLastUsedStep { get; set; }

    public bool IsLocked { get; set; }

    /// <summary>
    /// Random value embedded into every access token. Rotating it invalidates all
    /// access tokens of the user (logout everywhere, password change, lock).
    /// </summary>
    public string SecurityStamp { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public List<Role> Roles { get; set; } = [];
    public List<UserSession> Sessions { get; set; } = [];
    public List<ApiKey> ApiKeys { get; set; } = [];
}
