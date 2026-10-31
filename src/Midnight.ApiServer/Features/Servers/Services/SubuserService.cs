using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.ApiServer.Features.Users.Services;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>Other users with limited access to a server. The actor may only grant permissions they have themselves.</summary>
public class SubuserService
{
    private readonly DataContext _context;
    private readonly TimeProvider _timeProvider;

    public SubuserService(DataContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<List<SubuserResponse>> ListAsync(Server server, CancellationToken cancellationToken)
    {
        var subusers = await _context.Subusers
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.ServerId == server.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return subusers.Select(ToResponse).ToList();
    }

    public async Task<Subuser> CreateAsync(ServerAccessContext access, CreateSubuserRequest request, CancellationToken cancellationToken)
    {
        var permissions = ServerAccess.ValidateGrant(access.Permissions, request.Permissions);
        var email = UserService.NormalizeEmail(request.Email);

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken)
                   ?? throw new HttpApiException(StatusCodes.Status400BadRequest, "User not found", "There is no account with this email address")
                   {
                       Errors = new Dictionary<string, string> { ["Email"] = "There is no account with this email address" }
                   };

        if (user.Id == access.Server.OwnerId)
            throw HttpApiException.BadRequest("The owner can't be added as subuser");

        if (await _context.Subusers.AnyAsync(x => x.ServerId == access.Server.Id && x.UserId == user.Id, cancellationToken))
            throw HttpApiException.Conflict("This user is already a subuser of the server");

        var now = _timeProvider.GetUtcNow();
        var subuser = new Subuser { ServerId = access.Server.Id, User = user, UserId = user.Id, CreatedAt = now, UpdatedAt = now };
        subuser.SetPermissions(permissions);

        _context.Subusers.Add(subuser);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Parallel request for the same user, the unique index wins
            throw HttpApiException.Conflict("This user is already a subuser of the server");
        }

        return subuser;
    }

    public async Task<Subuser> UpdateAsync(ServerAccessContext access, int id, UpdateSubuserRequest request, CancellationToken cancellationToken)
    {
        var subuser = await GetAsync(access, id, cancellationToken);
        var permissions = ServerAccess.ValidateGrant(access.Permissions, request.Permissions);

        subuser.SetPermissions(permissions);
        subuser.UpdatedAt = _timeProvider.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);

        return subuser;
    }

    public async Task<Subuser> DeleteAsync(ServerAccessContext access, int id, CancellationToken cancellationToken)
    {
        var subuser = await GetAsync(access, id, cancellationToken);

        _context.Subusers.Remove(subuser);
        await _context.SaveChangesAsync(cancellationToken);

        return subuser;
    }

    /// <summary>Subusers can't change or remove their own entry</summary>
    private async Task<Subuser> GetAsync(ServerAccessContext access, int id, CancellationToken cancellationToken)
    {
        var subuser = await _context.Subusers.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id && x.ServerId == access.Server.Id, cancellationToken)
                      ?? throw HttpApiException.NotFound("Subuser not found");

        if (subuser.UserId == access.UserId)
            throw HttpApiException.Forbidden("You can't change your own permissions");

        return subuser;
    }

    public static SubuserResponse ToResponse(Subuser subuser) => new()
    {
        Id = subuser.Id,
        UserId = subuser.UserId,
        Username = subuser.User.Username,
        Email = subuser.User.Email,
        HasAvatar = !string.IsNullOrEmpty(subuser.User.AvatarFileName),
        Permissions = subuser.GetPermissions(),
        CreatedAt = subuser.CreatedAt
    };
}
