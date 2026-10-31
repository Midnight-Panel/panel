using System.ComponentModel.DataAnnotations;

namespace Midnight.Shared.Http.Account;

public class CurrentUserResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public bool HasAvatar { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public List<string> Permissions { get; set; } = [];

    /// <summary>Set while an administrator is impersonating this user.</summary>
    public string? ImpersonatedBy { get; set; }
}

public class UpdateProfileRequest
{
    [Required, MinLength(3), MaxLength(32), RegularExpression("^[a-zA-Z0-9_.-]+$")]
    public string Username { get; set; } = "";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";
}

public class ChangePasswordRequest
{
    [Required, MaxLength(256)]
    public string CurrentPassword { get; set; } = "";

    [Required, MinLength(8), MaxLength(256)]
    public string NewPassword { get; set; } = "";
}

public class TwoFactorSetupResponse
{
    public string Secret { get; set; } = "";

    /// <summary>otpauth:// uri, also rendered as QR code</summary>
    public string Uri { get; set; } = "";

    /// <summary>PNG data uri of the QR code</summary>
    public string QrCode { get; set; } = "";
}

public class TwoFactorEnableRequest
{
    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";
}

public class TwoFactorDisableRequest
{
    [Required, MaxLength(256)]
    public string Password { get; set; } = "";

    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";
}

public class SessionResponse
{
    public int Id { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public bool IsCurrent { get; set; }
}

public class ApiKeyResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Prefix { get; set; } = "";
    public List<string> Scopes { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

public class CreateApiKeyRequest
{
    [Required, MinLength(1), MaxLength(64)]
    public string Name { get; set; } = "";

    [MinLength(1)]
    public List<string> Scopes { get; set; } = [];

    public DateTimeOffset? ExpiresAt { get; set; }
}

public class CreateApiKeyResponse
{
    public ApiKeyResponse ApiKey { get; set; } = new();

    /// <summary>The full key. Only returned once on creation.</summary>
    public string Key { get; set; } = "";
}
