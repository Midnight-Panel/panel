using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.Shared;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.Users.Services;

public class RoleService
{
    private readonly DataContext _context;
    private readonly UserAuthCache _authCache;

    public RoleService(DataContext context, UserAuthCache authCache)
    {
        _context = context;
        _authCache = authCache;
    }

    public async Task<List<RoleResponse>> ListAsync(CancellationToken cancellationToken)
    {
        return await _context.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new RoleResponse
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Permissions = x.Permissions,
                UserCount = x.Users.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleResponse> CreateAsync(RoleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureUniqueAsync(name, null, cancellationToken);

        var role = new Role
        {
            Name = name,
            Description = request.Description?.Trim(),
            Permissions = ValidatePermissions(request.Permissions)
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);

        return ToResponse(role, 0);
    }

    public async Task<RoleResponse> UpdateAsync(int id, RoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _context.Roles.Include(x => x.Users).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                   ?? throw HttpApiException.NotFound("Role not found");

        var name = request.Name.Trim();
        await EnsureUniqueAsync(name, role.Id, cancellationToken);

        if (role.Name == UserService.AdministratorRoleName && name != role.Name)
            throw HttpApiException.BadRequest("The administrator role can't be renamed");

        role.Name = name;
        role.Description = request.Description?.Trim();
        role.Permissions = ValidatePermissions(request.Permissions);

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var user in role.Users)
            _authCache.Invalidate(user.Id);

        return ToResponse(role, role.Users.Count);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var role = await _context.Roles.Include(x => x.Users).FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                   ?? throw HttpApiException.NotFound("Role not found");

        if (role.Name == UserService.AdministratorRoleName)
            throw HttpApiException.BadRequest("The administrator role can't be deleted");

        var userIds = role.Users.Select(x => x.Id).ToList();

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var userId in userIds)
            _authCache.Invalidate(userId);
    }

    private static List<string> ValidatePermissions(IEnumerable<string> permissions)
    {
        var result = permissions
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();

        var invalid = result.FirstOrDefault(x => !Permissions.IsValid(x));

        if (invalid != null)
            throw HttpApiException.BadRequest("Invalid permission", $"'{invalid}' is not a valid permission");

        return result;
    }

    private async Task EnsureUniqueAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        var lower = name.ToLower();

        if (await _context.Roles.AnyAsync(x => x.Id != exceptId && x.Name.ToLower() == lower, cancellationToken))
            throw HttpApiException.Conflict("A role with this name already exists");
    }

    private static RoleResponse ToResponse(Role role, int userCount) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        Permissions = role.Permissions,
        UserCount = userCount
    };
}
