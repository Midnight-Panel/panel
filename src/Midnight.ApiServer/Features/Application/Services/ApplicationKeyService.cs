using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Application.Entities;
using Midnight.Shared;
using Midnight.Shared.Http.Application;

namespace Midnight.ApiServer.Features.Application.Services;

/// <summary>Manages application keys (/api/admin/application-keys)</summary>
public class ApplicationKeyService
{
    public const int MaxAllowedIps = 32;

    private readonly DataContext _context;
    private readonly TimeProvider _timeProvider;

    public ApplicationKeyService(DataContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<List<ApplicationKeyResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var keys = await _context.ApplicationKeys
            .AsNoTracking()
            .Include(x => x.CreatedBy)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return keys.Select(ToResponse).ToList();
    }

    /// <param name="actorPermissions">Permissions of the creating admin, see <see cref="EnsureCanGrant"/></param>
    public async Task<CreatedApplicationKeyResponse> CreateAsync(int actorId, IReadOnlyCollection<string> actorPermissions, ApplicationKeyRequest request, CancellationToken cancellationToken)
    {
        var (description, permissions, allowedIps) = Validate(request);

        EnsureCanGrant(actorPermissions, permissions, null);

        var identifier = SecureTokens.GenerateAlphanumeric(8);
        var token = $"{ApplicationKeyAuthenticationHandler.KeyPrefix}{identifier}_{SecureTokens.GenerateAlphanumeric(40)}";

        var key = new ApplicationKey
        {
            Identifier = identifier,
            KeyHash = SecureTokens.Hash(token),
            Description = description,
            AllowedIps = allowedIps,
            CreatedById = actorId,
            CreatedAt = _timeProvider.GetUtcNow()
        };

        key.SetPermissions(permissions);

        _context.ApplicationKeys.Add(key);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(key).Reference(x => x.CreatedBy).LoadAsync(cancellationToken);

        return new CreatedApplicationKeyResponse { Key = ToResponse(key), Token = token };
    }

    /// <summary>The token stays the same. Only raised access levels need the matching permissions of the actor.</summary>
    public async Task<ApplicationKeyResponse> UpdateAsync(int id, IReadOnlyCollection<string> actorPermissions, ApplicationKeyRequest request, CancellationToken cancellationToken)
    {
        var key = await _context.ApplicationKeys.Include(x => x.CreatedBy).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                  ?? throw HttpApiException.NotFound("Application key not found");

        var (description, permissions, allowedIps) = Validate(request);

        EnsureCanGrant(actorPermissions, permissions, key.GetPermissions());

        key.Description = description;
        key.AllowedIps = allowedIps;
        key.SetPermissions(permissions);

        await _context.SaveChangesAsync(cancellationToken);

        return ToResponse(key);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var key = await _context.ApplicationKeys.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                  ?? throw HttpApiException.NotFound("Application key not found");

        _context.ApplicationKeys.Remove(key);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The admin permissions needed to grant an access level on a resource</summary>
    public static string[] GetRequiredPermissions(ApplicationResource resource, ApplicationAccess access)
    {
        if (access == ApplicationAccess.None)
            return [];

        var (read, write) = resource switch
        {
            ApplicationResource.Users => (Permissions.AdminUsersRead, Permissions.AdminUsersWrite),
            ApplicationResource.Servers => (Permissions.AdminServersRead, Permissions.AdminServersWrite),
            ApplicationResource.Nodes => (Permissions.AdminNodesRead, Permissions.AdminNodesWrite),
            ApplicationResource.Allocations => (Permissions.AdminNodesRead, Permissions.AdminNodesWrite),
            ApplicationResource.Shards => (Permissions.AdminShardsRead, Permissions.AdminShardsWrite),
            ApplicationResource.DatabaseHosts => (Permissions.AdminServersRead, Permissions.AdminServersWrite),
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null)
        };

        return access == ApplicationAccess.ReadWrite ? [read, write] : [read];
    }

    /// <summary>
    /// An admin can only grant access he has himself (Servers ReadWrite needs admin.servers.read and .write).
    /// On updates only raised levels are checked, so an admin can edit the description of a key with more access.
    /// </summary>
    public static void EnsureCanGrant(IReadOnlyCollection<string> actorPermissions,
        IReadOnlyDictionary<ApplicationResource, ApplicationAccess> requested,
        IReadOnlyDictionary<ApplicationResource, ApplicationAccess>? current)
    {
        foreach (var (resource, access) in requested)
        {
            if (current != null && current.TryGetValue(resource, out var existing) && access <= existing)
                continue;

            var missing = GetRequiredPermissions(resource, access).Where(x => !Permissions.Has(actorPermissions, x)).ToList();

            if (missing.Count > 0)
            {
                throw new HttpApiException(StatusCodes.Status403Forbidden, $"You can't grant {access} access to {resource}",
                    $"Granting {access} on {resource} requires the permission(s) {string.Join(", ", missing)}")
                {
                    Errors = new Dictionary<string, string> { [$"Permissions.{resource}"] = $"You can't grant {access} access to {resource} without {string.Join(", ", missing)}" }
                };
            }
        }
    }

    /// <summary>Normalizes the request, every resource is present in the returned permissions</summary>
    public static (string Description, Dictionary<ApplicationResource, ApplicationAccess> Permissions, List<string> AllowedIps) Validate(ApplicationKeyRequest request)
    {
        var errors = new Dictionary<string, string>();

        var description = request.Description?.Trim() ?? "";

        if (description.Length == 0)
            errors["Description"] = "Enter a description";
        else if (description.Length > 255)
            errors["Description"] = "The description can have at most 255 characters";

        var permissions = Enum.GetValues<ApplicationResource>().ToDictionary(x => x, _ => ApplicationAccess.None);

        foreach (var (resource, access) in request.Permissions ?? [])
        {
            if (!Enum.IsDefined(resource))
                errors["Permissions"] = $"'{resource}' is not a known resource";
            else if (!Enum.IsDefined(access))
                errors[$"Permissions.{resource}"] = $"'{access}' is not a valid access level";
            else
                permissions[resource] = access;
        }

        var allowedIps = new List<string>();
        var rawIps = request.AllowedIps ?? [];

        if (rawIps.Count > MaxAllowedIps)
            errors["AllowedIps"] = $"At most {MaxAllowedIps} entries are allowed";

        foreach (var raw in rawIps.Take(MaxAllowedIps))
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            if (!IpRules.TryNormalize(raw, out var normalized))
            {
                errors["AllowedIps"] = $"'{raw.Trim()}' is not a valid IPv4/IPv6 address or CIDR range like 203.0.113.0/24";
                break;
            }

            if (!allowedIps.Contains(normalized))
                allowedIps.Add(normalized);
        }

        if (errors.Count > 0)
            throw new HttpApiException(StatusCodes.Status400BadRequest, "Invalid application key", string.Join(" ", errors.Values)) { Errors = errors };

        return (description, permissions, allowedIps);
    }

    public static ApplicationKeyResponse ToResponse(ApplicationKey key) => new()
    {
        Id = key.Id,
        Identifier = ApplicationKeyAuthenticationHandler.KeyPrefix + key.Identifier,
        Description = key.Description,
        Permissions = key.GetPermissions(),
        AllowedIps = key.AllowedIps.ToList(),
        CreatedById = key.CreatedById,
        CreatedByUsername = key.CreatedBy?.Username,
        CreatedAt = key.CreatedAt,
        LastUsedAt = key.LastUsedAt
    };
}
