using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Midnight.ApiServer.Core.Database;
using Midnight.Shared;

namespace Midnight.ApiServer.Core.Security;

public record UserAuthInfo(int Id, string Username, bool IsLocked, string SecurityStamp, string[] Permissions);

/// <summary>
/// Short lived cache of the data needed to validate every authenticated request
/// (lock state, security stamp, effective permissions). Services that change those values
/// call <see cref="Invalidate"/> so changes apply immediately.
/// </summary>
public class UserAuthCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public UserAuthCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task<UserAuthInfo?> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey(userId), out UserAuthInfo? cached))
            return cached;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        var user = await context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.Username,
                x.IsLocked,
                x.SecurityStamp,
                RolePermissions = x.Roles.Select(r => r.Permissions).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
            return null;

        var permissions = user.RolePermissions
            .SelectMany(x => x)
            .Concat(Permissions.UserDefaults)
            .Distinct()
            .ToArray();

        var info = new UserAuthInfo(user.Id, user.Username, user.IsLocked, user.SecurityStamp, permissions);

        _cache.Set(CacheKey(userId), info, CacheDuration);

        return info;
    }

    public void Invalidate(int userId) => _cache.Remove(CacheKey(userId));

    private static string CacheKey(int userId) => $"auth:user:{userId}";
}
