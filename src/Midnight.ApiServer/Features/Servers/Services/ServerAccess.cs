using System.Security.Claims;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.Shared;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>
/// Effective permissions of a user on a server (see ServerPermissions):
/// owner and admins with admin.servers.write → "*", admins with admin.servers.read → the read permissions,
/// subusers → exactly their list. Users without any access get 404, so nothing about the server is leaked.
/// Api keys without account.write are reduced to the read permissions.
/// </summary>
public static class ServerAccess
{
    /// <summary>What admins with admin.servers.read (and read only api keys) may do</summary>
    public static readonly IReadOnlyList<string> ReadPermissions = ServerPermissions.AllPermissions
        .Where(x => x.EndsWith(".read", StringComparison.Ordinal) ||
                    x is ServerPermissions.FileReadContent or ServerPermissions.WebsocketConnect or ServerPermissions.ActivityRead)
        .Distinct()
        .ToList();

    private static readonly IReadOnlyList<string> AllPermissions = [ServerPermissions.All];

    public static bool IsOwner(ClaimsPrincipal user, Server server)
        => server.OwnerId == user.GetUserId();

    /// <summary>Null when the user has no access at all</summary>
    /// <param name="subuserPermissions">Permissions of the user's subuser entry, null if they are no subuser</param>
    public static IReadOnlyList<string>? GetPermissions(ClaimsPrincipal user, Server server, IReadOnlyCollection<string>? subuserPermissions)
    {
        IReadOnlyList<string>? permissions;

        if (IsOwner(user, server) || user.HasPermission(Permissions.AdminServersWrite))
        {
            permissions = AllPermissions;
        }
        else
        {
            var isAdmin = user.HasPermission(Permissions.AdminServersRead);

            if (subuserPermissions == null && !isAdmin)
                return null;

            permissions = (subuserPermissions ?? [])
                .Where(ServerPermissions.IsValid)
                .Concat(isAdmin ? ReadPermissions : [])
                .Distinct()
                .Order(StringComparer.Ordinal)
                .ToList();
        }

        // Read only api keys (and sessions without account.write) can't change anything
        if (!user.HasPermission(Permissions.AccountWrite))
            permissions = ReadPermissions.Where(x => ServerPermissions.Has(permissions, x)).ToList();

        return permissions;
    }

    public static void EnsurePermission(IReadOnlyCollection<string> granted, string permission)
    {
        if (!ServerPermissions.Has(granted, permission))
            throw HttpApiException.Forbidden("Missing permission", $"You need the permission '{permission}' on this server");
    }

    /// <summary>The permission needed for a power action</summary>
    public static string GetPowerPermission(Shared.Enums.PowerAction action) => action switch
    {
        Shared.Enums.PowerAction.Start => ServerPermissions.ControlStart,
        Shared.Enums.PowerAction.Restart => ServerPermissions.ControlRestart,
        _ => ServerPermissions.ControlStop
    };

    /// <summary>
    /// Validates permissions for a subuser: every entry must be a known permission (no wildcards) and the actor
    /// may only grant permissions they have themselves. Returns the normalized list.
    /// </summary>
    public static List<string> ValidateGrant(IReadOnlyCollection<string> actorPermissions, IEnumerable<string> requested)
    {
        var permissions = requested.Select(x => x?.Trim() ?? "").Distinct().ToList();

        var invalid = permissions.Where(x => !ServerPermissions.IsValid(x)).ToList();

        if (invalid.Count > 0)
        {
            throw new HttpApiException(StatusCodes.Status400BadRequest, "Invalid permissions", $"Unknown permission(s): {string.Join(", ", invalid)}")
            {
                Errors = new Dictionary<string, string> { ["Permissions"] = $"Unknown permission(s): {string.Join(", ", invalid)}" }
            };
        }

        var missing = permissions.Where(x => !ServerPermissions.Has(actorPermissions, x)).ToList();

        if (missing.Count > 0)
            throw HttpApiException.Forbidden("You can't grant permissions you don't have", $"Missing: {string.Join(", ", missing)}");

        return permissions.Order(StringComparer.Ordinal).ToList();
    }
}

/// <summary>A server loaded for a request together with the permissions of the current user</summary>
public sealed class ServerAccessContext(Server server, IReadOnlyList<string> permissions, int userId)
{
    public Server Server { get; } = server;
    public IReadOnlyList<string> Permissions { get; } = permissions;
    public int UserId { get; } = userId;

    public bool Has(string permission) => ServerPermissions.Has(Permissions, permission);

    public void Ensure(string permission) => ServerAccess.EnsurePermission(Permissions, permission);
}
