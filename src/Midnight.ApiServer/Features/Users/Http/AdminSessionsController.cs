using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.Users.Http;

/// <summary>Active login sessions of all users</summary>
[ApiController]
[Route("api/admin/sessions")]
public class AdminSessionsController : ControllerBase
{
    private readonly DataContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminSessionsController> _logger;

    public AdminSessionsController(DataContext context, TimeProvider timeProvider, ILogger<AdminSessionsController> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminUsersRead)]
    public async Task<PagedResult<AdminSessionResponse>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var sessions = _context.UserSessions
            .AsNoTracking()
            .Where(x => x.RevokedAt == null && x.ExpiresAt > now);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            sessions = sessions.Where(x => x.User.Username.ToLower().Contains(search) || x.User.Email.Contains(search) || (x.IpAddress != null && x.IpAddress.Contains(search)));
        }

        sessions = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("createdat", false) => sessions.OrderBy(x => x.CreatedAt),
            ("createdat", true) => sessions.OrderByDescending(x => x.CreatedAt),
            ("lastusedat", false) => sessions.OrderBy(x => x.LastUsedAt),
            _ => sessions.OrderByDescending(x => x.LastUsedAt)
        };

        var total = await sessions.CountAsync(cancellationToken);

        var items = await sessions
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(x => new AdminSessionResponse
            {
                Id = x.Id,
                UserId = x.UserId,
                Username = x.User.Username,
                Email = x.User.Email,
                IpAddress = x.IpAddress,
                UserAgent = x.UserAgent,
                CreatedAt = x.CreatedAt,
                LastUsedAt = x.LastUsedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminSessionResponse>
        {
            Items = items,
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            TotalCount = total
        };
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminUsersWrite)]
    public async Task<IActionResult> Revoke(int id, CancellationToken cancellationToken)
    {
        var session = await _context.UserSessions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                      ?? throw HttpApiException.NotFound("Session not found");

        session.RevokedAt ??= _timeProvider.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {ActorId} revoked session {SessionId} of user {UserId}", User.GetUserId(), id, session.UserId);

        return NoContent();
    }
}
