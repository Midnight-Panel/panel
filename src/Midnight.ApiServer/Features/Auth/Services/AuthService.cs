using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Mail;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.ApiServer.Features.Users.Services;
using Midnight.Shared.Http.Auth;

namespace Midnight.ApiServer.Features.Auth.Services;

public record ClientInfo(string? IpAddress, string? UserAgent);

/// <summary>Result of a successful authentication: the response body plus the refresh token for the cookie</summary>
public record AuthResult(LoginResponse Response, string? RefreshToken, DateTimeOffset? RefreshTokenExpiresAt);

public class AuthService
{
    private readonly DataContext _context;
    private readonly PasswordHasher _passwordHasher;
    private readonly AccessTokenService _accessTokenService;
    private readonly TotpService _totpService;
    private readonly UserService _userService;
    private readonly UserAuthCache _authCache;
    private readonly IMailSender _mailSender;
    private readonly IOptionsMonitor<AppConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;

    // Used to spend the same time on unknown emails as on wrong passwords
    private static string? _dummyHash;

    public AuthService(
        DataContext context,
        PasswordHasher passwordHasher,
        AccessTokenService accessTokenService,
        TotpService totpService,
        UserService userService,
        UserAuthCache authCache,
        IMailSender mailSender,
        IOptionsMonitor<AppConfiguration> configuration,
        TimeProvider timeProvider,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _accessTokenService = accessTokenService;
        _totpService = totpService;
        _userService = userService;
        _authCache = authCache;
        _mailSender = mailSender;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private AuthenticationConfiguration AuthConfig => _configuration.CurrentValue.Authentication;

    public async Task<AuthResult> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user == null)
        {
            _dummyHash ??= _passwordHasher.Hash(SecureTokens.Generate());
            _passwordHasher.Verify(request.Password, _dummyHash);

            throw HttpApiException.Unauthorized("Invalid email or password");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogInformation("Failed login for user {UserId} from {Ip}", user.Id, client.IpAddress);
            throw HttpApiException.Unauthorized("Invalid email or password");
        }

        if (user.IsLocked)
            throw HttpApiException.Forbidden("Account locked", "This account has been locked by an administrator");

        if (user.TotpEnabled && user.TotpSecret != null)
        {
            if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                return new AuthResult(new LoginResponse { RequiresTwoFactor = true }, null, null);

            var secret = _totpService.Unprotect(user.TotpSecret);

            if (!_totpService.Verify(secret, request.TwoFactorCode, user.TotpLastUsedStep, out var step))
                throw HttpApiException.Unauthorized("Invalid two factor code");

            user.TotpLastUsedStep = step;
        }

        if (_passwordHasher.NeedsRehash(user.PasswordHash))
            user.PasswordHash = _passwordHasher.Hash(request.Password);

        user.LastLoginAt = _timeProvider.GetUtcNow();

        return await CreateSessionAsync(user, client, cancellationToken);
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var isFirstUser = !await _context.Users.AnyAsync(cancellationToken);

        if (!isFirstUser && !AuthConfig.EnableRegistration)
            throw HttpApiException.Forbidden("Registration is disabled");

        var user = await _userService.CreateAsync(request.Username, request.Email, request.Password, [], cancellationToken);

        // Concurrent registrations on a fresh install could both see "no users yet",
        // only the user with the lowest id becomes administrator
        if (isFirstUser && await _context.Users.AnyAsync(x => x.Id < user.Id, cancellationToken))
            isFirstUser = false;

        if (isFirstUser)
        {
            await _userService.AssignAdministratorRoleAsync(user, cancellationToken);
            _logger.LogInformation("First user {UserId} registered and was made administrator", user.Id);
        }

        user.LastLoginAt = _timeProvider.GetUtcNow();

        return await CreateSessionAsync(user, client, cancellationToken);
    }

    /// <summary>Exchanges a refresh token for a new access token and rotates the refresh token</summary>
    public async Task<AuthResult> RefreshAsync(string refreshToken, ClientInfo client, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var hash = SecureTokens.Hash(refreshToken);

        var session = await _context.UserSessions
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.RefreshTokenHash == hash, cancellationToken);

        if (session == null || session.RevokedAt != null || session.ExpiresAt <= now || session.User.IsLocked)
            throw HttpApiException.Unauthorized("Session expired");

        var newRefreshToken = SecureTokens.Generate();

        session.RefreshTokenHash = SecureTokens.Hash(newRefreshToken);
        session.LastUsedAt = now;
        session.IpAddress = client.IpAddress;
        session.UserAgent = Truncate(client.UserAgent, 512);

        await _context.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAt) = _accessTokenService.Create(
            session.UserId,
            session.User.SecurityStamp,
            session.Id,
            null,
            TimeSpan.FromMinutes(AuthConfig.AccessTokenMinutes));

        return new AuthResult(
            new LoginResponse { AccessToken = accessToken, ExpiresAt = expiresAt },
            newRefreshToken,
            session.ExpiresAt);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = SecureTokens.Hash(refreshToken);
        var session = await _context.UserSessions.FirstOrDefaultAsync(x => x.RefreshTokenHash == hash, cancellationToken);

        if (session == null || session.RevokedAt != null)
            return;

        session.RevokedAt = _timeProvider.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Always succeeds so the endpoint can't be used to find out which emails are registered</summary>
    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user == null || user.IsLocked)
            return;

        var now = _timeProvider.GetUtcNow();
        var token = SecureTokens.Generate();

        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = SecureTokens.Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(AuthConfig.PasswordResetMinutes)
        });

        await _context.SaveChangesAsync(cancellationToken);

        var link = $"{_configuration.CurrentValue.PublicUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

        await _mailSender.SendAsync(
            user.Email,
            "Reset your password",
            $"Hello {user.Username},\n\nuse the following link to set a new password. It is valid for {AuthConfig.PasswordResetMinutes} minutes.\n\n{link}\n\nIf you did not request this, you can ignore this mail.",
            cancellationToken);
    }

    public async Task ConfirmPasswordResetAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var hash = SecureTokens.Hash(token);

        var resetToken = await _context.PasswordResetTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (resetToken == null || resetToken.UsedAt != null || resetToken.ExpiresAt <= now)
            throw HttpApiException.BadRequest("Invalid or expired reset link");

        // Invalidates this and every other outstanding reset link of the user
        var openTokens = await _context.PasswordResetTokens
            .Where(x => x.UserId == resetToken.UserId && x.UsedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var openToken in openTokens)
            openToken.UsedAt = now;

        await _userService.SetPasswordAsync(resetToken.User, newPassword, cancellationToken);
    }

    private async Task<AuthResult> CreateSessionAsync(User user, ClientInfo client, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var refreshToken = SecureTokens.Generate();

        var session = new UserSession
        {
            User = user,
            RefreshTokenHash = SecureTokens.Hash(refreshToken),
            IpAddress = client.IpAddress,
            UserAgent = Truncate(client.UserAgent, 512),
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.AddDays(AuthConfig.RefreshTokenDays)
        };

        _context.UserSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        _authCache.Invalidate(user.Id);

        var (accessToken, expiresAt) = _accessTokenService.Create(
            user.Id,
            user.SecurityStamp,
            session.Id,
            null,
            TimeSpan.FromMinutes(AuthConfig.AccessTokenMinutes));

        return new AuthResult(
            new LoginResponse { AccessToken = accessToken, ExpiresAt = expiresAt },
            refreshToken,
            session.ExpiresAt);
    }

    private static string? Truncate(string? value, int maxLength)
        => value == null || value.Length <= maxLength ? value : value[..maxLength];
}
