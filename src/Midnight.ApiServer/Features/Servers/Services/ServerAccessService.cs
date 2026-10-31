using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.ApiServer.Core.Security;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>Loads a server for the current user and checks their effective permissions (see <see cref="ServerAccess"/>)</summary>
public class ServerAccessService
{
    private readonly ServerService _serverService;
    private readonly DataContext _context;

    public ServerAccessService(ServerService serverService, DataContext context)
    {
        _serverService = serverService;
        _context = context;
    }

    /// <summary>404 without any access, 403 when the permission is missing</summary>
    /// <param name="permission">Required ServerPermissions entry, null = any access</param>
    public async Task<ServerAccessContext> GetAsync(ClaimsPrincipal user, Guid uuid, string? permission, CancellationToken cancellationToken)
    {
        var server = await _serverService.GetAsync(uuid, cancellationToken);
        var permissions = await GetPermissionsAsync(user, server, cancellationToken)
                          ?? throw HttpApiException.NotFound("Server not found");

        var access = new ServerAccessContext(server, permissions, user.GetUserId());

        if (permission != null)
            access.Ensure(permission);

        return access;
    }

    /// <summary>Null when the user has no access</summary>
    public async Task<IReadOnlyList<string>?> GetPermissionsAsync(ClaimsPrincipal user, Server server, CancellationToken cancellationToken)
    {
        List<string>? subuserPermissions = null;

        if (!ServerAccess.IsOwner(user, server))
        {
            var userId = user.GetUserId();

            var json = await _context.Subusers
                .AsNoTracking()
                .Where(x => x.ServerId == server.Id && x.UserId == userId)
                .Select(x => x.PermissionsJson)
                .FirstOrDefaultAsync(cancellationToken);

            if (json != null)
                subuserPermissions = new Subuser { PermissionsJson = json }.GetPermissions();
        }

        return ServerAccess.GetPermissions(user, server, subuserPermissions);
    }
}
