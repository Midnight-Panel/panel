using System.ComponentModel.DataAnnotations;

namespace Midnight.Shared.Http.Auth;

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, MaxLength(256)]
    public string Password { get; set; } = "";

    /// <summary>Required when the account has 2FA enabled. Without it the login returns RequiresTwoFactor.</summary>
    [MaxLength(16)]
    public string? TwoFactorCode { get; set; }
}

public class RegisterRequest
{
    [Required, MinLength(3), MaxLength(32), RegularExpression("^[a-zA-Z0-9_.-]+$")]
    public string Username { get; set; } = "";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, MinLength(8), MaxLength(256)]
    public string Password { get; set; } = "";
}

/// <summary>
/// Returned by login, register and refresh. The refresh token itself is never part of the body,
/// it is stored in an http only cookie.
/// </summary>
public class LoginResponse
{
    public bool RequiresTwoFactor { get; set; }
    public string? AccessToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class PasswordResetRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";
}

public class PasswordResetConfirmRequest
{
    [Required, MaxLength(256)]
    public string Token { get; set; } = "";

    [Required, MinLength(8), MaxLength(256)]
    public string NewPassword { get; set; } = "";
}

public class PublicSettingsResponse
{
    public string AppName { get; set; } = "Midnight";
    public string Version { get; set; } = "";
    public bool RegistrationEnabled { get; set; }

    /// <summary>True if no user exists yet. The first registered user becomes administrator.</summary>
    public bool SetupRequired { get; set; }
}
