using System.ComponentModel.DataAnnotations;

namespace Midnight.Shared.Http.Admin;

public class AdminUserResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public bool HasAvatar { get; set; }
    public bool IsLocked { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public List<int> RoleIds { get; set; } = [];
    public List<string> RoleNames { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public class CreateUserRequest
{
    [Required, MinLength(3), MaxLength(32), RegularExpression("^[a-zA-Z0-9_.-]+$")]
    public string Username { get; set; } = "";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, MinLength(8), MaxLength(256)]
    public string Password { get; set; } = "";

    public List<int> RoleIds { get; set; } = [];
}

public class UpdateUserRequest
{
    [Required, MinLength(3), MaxLength(32), RegularExpression("^[a-zA-Z0-9_.-]+$")]
    public string Username { get; set; } = "";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    /// <summary>Optional, only changed if set</summary>
    [MinLength(8), MaxLength(256)]
    public string? Password { get; set; }

    public bool IsLocked { get; set; }

    public List<int> RoleIds { get; set; } = [];
}

public class ImpersonationResponse
{
    public string AccessToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}

public class RoleResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = [];
    public int UserCount { get; set; }
}

public class RoleRequest
{
    [Required, MinLength(1), MaxLength(64)]
    public string Name { get; set; } = "";

    [MaxLength(256)]
    public string? Description { get; set; }

    public List<string> Permissions { get; set; } = [];
}

public class AdminOverviewResponse
{
    public int UserCount { get; set; }
    public int LockedUserCount { get; set; }
    public int ServerCount { get; set; }
    public int NodeCount { get; set; }
    public string Version { get; set; } = "";
    public TimeSpan Uptime { get; set; }
    public long MemoryUsageBytes { get; set; }
    public string OperatingSystem { get; set; } = "";
    public string Runtime { get; set; } = "";
}

public class AdminSessionResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
}
