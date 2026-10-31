using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Auth.Services;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.ApiServer.Features.Users.Services;
using Midnight.Shared.Http.Account;

namespace Midnight.ApiServer.Features.Account.Services;

public class AccountService
{
    private readonly DataContext _context;
    private readonly UserService _userService;
    private readonly UserAuthCache _authCache;
    private readonly PasswordHasher _passwordHasher;
    private readonly TotpService _totpService;
    private readonly IOptionsMonitor<AppConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;

    public AccountService(
        DataContext context,
        UserService userService,
        UserAuthCache authCache,
        PasswordHasher passwordHasher,
        TotpService totpService,
        IOptionsMonitor<AppConfiguration> configuration,
        TimeProvider timeProvider)
    {
        _context = context;
        _userService = userService;
        _authCache = authCache;
        _passwordHasher = passwordHasher;
        _totpService = totpService;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<CurrentUserResponse> GetCurrentAsync(int userId, int? impersonatorId, IEnumerable<string> permissions, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        string? impersonatedBy = null;

        if (impersonatorId.HasValue)
        {
            impersonatedBy = await _context.Users
                .Where(x => x.Id == impersonatorId.Value)
                .Select(x => x.Username)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new CurrentUserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            HasAvatar = user.AvatarFileName != null,
            TwoFactorEnabled = user.TotpEnabled,
            Permissions = permissions.Distinct().ToList(),
            ImpersonatedBy = impersonatedBy
        };
    }

    public async Task UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        var username = request.Username.Trim();
        var email = UserService.NormalizeEmail(request.Email);

        await _userService.EnsureUniqueAsync(username, email, user.Id, cancellationToken);

        user.Username = username;
        user.Email = email;

        await _context.SaveChangesAsync(cancellationToken);
        _authCache.Invalidate(user.Id);
    }

    public async Task ChangePasswordAsync(int userId, int? currentSessionId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw HttpApiException.BadRequest("The current password is wrong");

        await _userService.SetPasswordAsync(user, request.NewPassword, cancellationToken, currentSessionId);
    }

    public async Task<TwoFactorSetupResponse> BeginTwoFactorSetupAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        if (user.TotpEnabled)
            throw HttpApiException.Conflict("Two factor authentication is already enabled");

        var secret = _totpService.GenerateSecret();

        user.PendingTotpSecret = _totpService.Protect(secret);
        await _context.SaveChangesAsync(cancellationToken);

        var uri = TotpService.BuildUri(secret, user.Email, _configuration.CurrentValue.AppName);

        return new TwoFactorSetupResponse
        {
            Secret = secret,
            Uri = uri,
            QrCode = TotpService.RenderQrCode(uri)
        };
    }

    public async Task EnableTwoFactorAsync(int userId, string code, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        if (user.TotpEnabled)
            throw HttpApiException.Conflict("Two factor authentication is already enabled");

        if (user.PendingTotpSecret == null)
            throw HttpApiException.BadRequest("Start the two factor setup first");

        var secret = _totpService.Unprotect(user.PendingTotpSecret);

        if (!_totpService.Verify(secret, code, 0, out var step))
            throw HttpApiException.BadRequest("Invalid two factor code");

        user.TotpSecret = user.PendingTotpSecret;
        user.PendingTotpSecret = null;
        user.TotpLastUsedStep = step;
        user.TotpEnabled = true;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableTwoFactorAsync(int userId, TwoFactorDisableRequest request, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        if (!user.TotpEnabled || user.TotpSecret == null)
            throw HttpApiException.BadRequest("Two factor authentication is not enabled");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw HttpApiException.BadRequest("The password is wrong");

        if (!_totpService.Verify(_totpService.Unprotect(user.TotpSecret), request.Code, user.TotpLastUsedStep, out _))
            throw HttpApiException.BadRequest("Invalid two factor code");

        user.TotpEnabled = false;
        user.TotpSecret = null;
        user.TotpLastUsedStep = 0;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<SessionResponse>> ListSessionsAsync(int userId, int? currentSessionId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var sessions = await _context.UserSessions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.LastUsedAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(x => new SessionResponse
        {
            Id = x.Id,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            CreatedAt = x.CreatedAt,
            LastUsedAt = x.LastUsedAt,
            IsCurrent = x.Id == currentSessionId
        }).ToList();
    }

    public async Task RevokeSessionAsync(int userId, int sessionId, CancellationToken cancellationToken)
    {
        var session = await _context.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, cancellationToken)
                      ?? throw HttpApiException.NotFound("Session not found");

        session.RevokedAt ??= _timeProvider.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Logs out every session including the current one and invalidates all access tokens</summary>
    public async Task RevokeAllSessionsAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId, cancellationToken);

        await _userService.RevokeAllSessionsAsync(user, null, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _authCache.Invalidate(user.Id);
    }

    private async Task<User> GetUserAsync(int userId, CancellationToken cancellationToken)
    {
        return await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
               ?? throw HttpApiException.NotFound("User not found");
    }
}
