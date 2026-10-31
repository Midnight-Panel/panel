using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.Users.Services;

public class UserService
{
    public const string AdministratorRoleName = "Administrators";

    private readonly DataContext _context;
    private readonly PasswordHasher _passwordHasher;
    private readonly UserAuthCache _authCache;
    private readonly TimeProvider _timeProvider;

    public UserService(DataContext context, PasswordHasher passwordHasher, UserAuthCache authCache, TimeProvider timeProvider)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _authCache = authCache;
        _timeProvider = timeProvider;
    }

    public async Task<User> CreateAsync(string username, string email, string password, IEnumerable<int> roleIds, CancellationToken cancellationToken)
    {
        username = username.Trim();
        email = NormalizeEmail(email);

        await EnsureUniqueAsync(username, email, null, cancellationToken);

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.Hash(password),
            SecurityStamp = SecureTokens.Generate(16),
            CreatedAt = _timeProvider.GetUtcNow(),
            Roles = await LoadRolesAsync(roleIds, cancellationToken)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<PagedResult<AdminUserResponse>> ListAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        var users = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            users = users.Where(x => x.Username.ToLower().Contains(search) || x.Email.Contains(search));
        }

        users = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("username", false) => users.OrderBy(x => x.Username),
            ("username", true) => users.OrderByDescending(x => x.Username),
            ("email", false) => users.OrderBy(x => x.Email),
            ("email", true) => users.OrderByDescending(x => x.Email),
            ("createdat", false) => users.OrderBy(x => x.CreatedAt),
            ("createdat", true) => users.OrderByDescending(x => x.CreatedAt),
            (_, true) => users.OrderByDescending(x => x.Id),
            _ => users.OrderBy(x => x.Id)
        };

        var total = await users.CountAsync(cancellationToken);

        var items = await users
            .Include(x => x.Roles)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminUserResponse>
        {
            Items = items.Select(ToAdminResponse).ToList(),
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            TotalCount = total
        };
    }

    public async Task<User> GetAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Users
                   .Include(x => x.Roles)
                   .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
               ?? throw HttpApiException.NotFound("User not found");
    }

    public async Task<User> UpdateAsync(int id, UpdateUserRequest request, int actingUserId, CancellationToken cancellationToken)
    {
        var user = await GetAsync(id, cancellationToken);

        var username = request.Username.Trim();
        var email = NormalizeEmail(request.Email);

        await EnsureUniqueAsync(username, email, user.Id, cancellationToken);

        if (user.Id == actingUserId && request.IsLocked)
            throw HttpApiException.BadRequest("You can't lock your own account");

        var newRoles = await LoadRolesAsync(request.RoleIds, cancellationToken);

        if (user.Id == actingUserId && user.Roles.Any(x => x.Name == AdministratorRoleName) && newRoles.All(x => x.Name != AdministratorRoleName))
            throw HttpApiException.BadRequest("You can't remove the administrator role from yourself");

        user.Username = username;
        user.Email = email;
        user.Roles = newRoles;

        var invalidateTokens = false;

        if (user.IsLocked != request.IsLocked)
        {
            user.IsLocked = request.IsLocked;
            invalidateTokens = request.IsLocked;
        }

        if (!string.IsNullOrEmpty(request.Password))
        {
            user.PasswordHash = _passwordHasher.Hash(request.Password);
            invalidateTokens = true;
        }

        if (invalidateTokens)
            await RevokeAllSessionsAsync(user, null, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        _authCache.Invalidate(user.Id);

        return user;
    }

    public async Task DeleteAsync(int id, int actingUserId, CancellationToken cancellationToken)
    {
        if (id == actingUserId)
            throw HttpApiException.BadRequest("You can't delete your own account");

        var user = await GetAsync(id, cancellationToken);

        // TODO: Phase 3 - refuse deletion while the user still owns servers

        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);

        _authCache.Invalidate(user.Id);
    }

    public async Task ResetTwoFactorAsync(int id, CancellationToken cancellationToken)
    {
        var user = await GetAsync(id, cancellationToken);

        user.TotpEnabled = false;
        user.TotpSecret = null;
        user.PendingTotpSecret = null;
        user.TotpLastUsedStep = 0;

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Sets a new password and logs out every session except <paramref name="keepSessionId"/></summary>
    public async Task SetPasswordAsync(User user, string password, CancellationToken cancellationToken, int? keepSessionId = null)
    {
        user.PasswordHash = _passwordHasher.Hash(password);

        await RevokeAllSessionsAsync(user, keepSessionId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _authCache.Invalidate(user.Id);
    }

    /// <summary>
    /// Revokes refresh tokens and rotates the security stamp so existing access tokens stop working.
    /// The session to keep gets a working access token again on its next refresh.
    /// Changes are saved by the caller.
    /// </summary>
    public async Task RevokeAllSessionsAsync(User user, int? keepSessionId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var sessions = await _context.UserSessions
            .Where(x => x.UserId == user.Id && x.RevokedAt == null && x.Id != keepSessionId)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            session.RevokedAt = now;

        user.SecurityStamp = SecureTokens.Generate(16);
    }

    public async Task AssignAdministratorRoleAsync(User user, CancellationToken cancellationToken)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(x => x.Name == AdministratorRoleName, cancellationToken);

        if (role == null)
        {
            role = new Role
            {
                Name = AdministratorRoleName,
                Description = "Full access to the panel",
                Permissions = [Permissions.Everything]
            };

            _context.Roles.Add(role);
        }

        await _context.Entry(user).Collection(x => x.Roles).LoadAsync(cancellationToken);

        if (!user.Roles.Contains(role))
            user.Roles.Add(role);

        await _context.SaveChangesAsync(cancellationToken);
        _authCache.Invalidate(user.Id);
    }

    public static AdminUserResponse ToAdminResponse(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        HasAvatar = user.AvatarFileName != null,
        IsLocked = user.IsLocked,
        TwoFactorEnabled = user.TotpEnabled,
        RoleIds = user.Roles.Select(x => x.Id).ToList(),
        RoleNames = user.Roles.Select(x => x.Name).ToList(),
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt
    };

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task EnsureUniqueAsync(string username, string email, int? exceptUserId, CancellationToken cancellationToken)
    {
        var usernameLower = username.ToLower();

        if (await _context.Users.AnyAsync(x => x.Id != exceptUserId && x.Username.ToLower() == usernameLower, cancellationToken))
            throw HttpApiException.Conflict("Username is already taken");

        if (await _context.Users.AnyAsync(x => x.Id != exceptUserId && x.Email == email, cancellationToken))
            throw HttpApiException.Conflict("Email is already in use");
    }

    private async Task<List<Role>> LoadRolesAsync(IEnumerable<int> roleIds, CancellationToken cancellationToken)
    {
        var ids = roleIds.Distinct().ToList();

        if (ids.Count == 0)
            return [];

        var roles = await _context.Roles.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

        if (roles.Count != ids.Count)
            throw HttpApiException.BadRequest("One or more roles do not exist");

        return roles;
    }
}
