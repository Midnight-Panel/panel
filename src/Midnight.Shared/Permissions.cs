namespace Midnight.Shared;

/// <summary>
/// All permission strings known to the panel. Permissions are dot separated and support
/// trailing wildcards: "admin.*" grants every permission starting with "admin.", "*" grants everything.
/// </summary>
public static class Permissions
{
    public const string Everything = "*";

    // Granted implicitly to every logged in user (not to api keys, those use scopes)
    public const string AccountRead = "account.read";
    public const string AccountWrite = "account.write";

    // Password, 2FA, sessions and api keys. Never granted to api keys.
    public const string AccountSecurity = "account.security";

    public const string AdminOverview = "admin.overview";

    public const string AdminUsersRead = "admin.users.read";
    public const string AdminUsersWrite = "admin.users.write";
    public const string AdminUsersImpersonate = "admin.users.impersonate";

    public const string AdminRolesRead = "admin.roles.read";
    public const string AdminRolesWrite = "admin.roles.write";

    public const string AdminSystemRead = "admin.system.read";
    public const string AdminSystemWrite = "admin.system.write";

    public const string AdminNodesRead = "admin.nodes.read";
    public const string AdminNodesWrite = "admin.nodes.write";

    public const string AdminShardsRead = "admin.shards.read";
    public const string AdminShardsWrite = "admin.shards.write";

    public const string AdminExtensionsRead = "admin.extensions.read";
    public const string AdminExtensionsWrite = "admin.extensions.write";

    public const string AdminImagesRead = "admin.images.read";
    public const string AdminImagesWrite = "admin.images.write";

    public const string AdminServersRead = "admin.servers.read";
    public const string AdminServersWrite = "admin.servers.write";

    /// <summary>Permissions every authenticated user has without any role.</summary>
    public static readonly string[] UserDefaults =
    [
        AccountRead,
        AccountWrite,
        AccountSecurity
    ];

    /// <summary>Grouped list used by the admin UI and the api key scope picker.</summary>
    public static readonly PermissionGroup[] Groups =
    [
        new("account", [AccountRead, AccountWrite]),
        new("admin.overview", [AdminOverview]),
        new("admin.users", [AdminUsersRead, AdminUsersWrite, AdminUsersImpersonate]),
        new("admin.roles", [AdminRolesRead, AdminRolesWrite]),
        new("admin.system", [AdminSystemRead, AdminSystemWrite]),
        new("admin.nodes", [AdminNodesRead, AdminNodesWrite]),
        new("admin.shards", [AdminShardsRead, AdminShardsWrite]),
        new("admin.extensions", [AdminExtensionsRead, AdminExtensionsWrite]),
        new("admin.images", [AdminImagesRead, AdminImagesWrite]),
        new("admin.servers", [AdminServersRead, AdminServersWrite])
    ];

    public static IEnumerable<string> All => Groups.SelectMany(x => x.Permissions);

    /// <summary>
    /// Checks whether the granted permission list contains the required permission,
    /// honoring wildcards.
    /// </summary>
    public static bool Has(IEnumerable<string> granted, string required)
    {
        foreach (var permission in granted)
        {
            if (Matches(permission, required))
                return true;
        }

        return false;
    }

    public static bool Matches(string granted, string required)
    {
        if (string.IsNullOrWhiteSpace(granted))
            return false;

        if (granted == Everything)
            return true;

        if (granted.EndsWith(".*", StringComparison.Ordinal))
        {
            // "admin.*" matches "admin.users.read" and "admin" itself
            var prefix = granted[..^2];

            return required.Equals(prefix, StringComparison.Ordinal) ||
                   required.StartsWith(prefix + ".", StringComparison.Ordinal);
        }

        return granted.Equals(required, StringComparison.Ordinal);
    }

    /// <summary>A permission string is valid if it consists of lowercase segments and an optional trailing wildcard.</summary>
    public static bool IsValid(string permission)
    {
        if (permission == Everything)
            return true;

        var segments = permission.Split('.');

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            if (segment == "*" && i == segments.Length - 1 && i > 0)
                continue;

            if (segment.Length == 0 || !segment.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
                return false;
        }

        return true;
    }
}

public record PermissionGroup(string Name, string[] Permissions);
