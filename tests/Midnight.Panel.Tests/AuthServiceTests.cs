using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Mail;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Auth.Services;
using Midnight.ApiServer.Features.Users.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Auth;
using OtpNet;

namespace Midnight.Panel.Tests;

/// <summary>Runs the auth flows against an in-memory sqlite database</summary>
public sealed class AuthServiceTests : IAsyncLifetime
{
    private static readonly ClientInfo Client = new("127.0.0.1", "tests");

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CapturingMailSender _mails = new();
    private readonly AppConfiguration _configuration = new();
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        _configuration.Security.JwtSecret = Convert.ToBase64String(new byte[64]);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMemoryCache();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<AppConfiguration>().Configure(options =>
        {
            options.Authentication = _configuration.Authentication;
            options.Security = _configuration.Security;
        });

        services.AddDbContext<SqliteDataContext>(options => options.UseSqlite(_connection));
        services.AddScoped<DataContext>(provider => provider.GetRequiredService<SqliteDataContext>());

        services.AddSingleton(new PasswordHasher(1024, 1, 1));
        services.AddSingleton<UserAuthCache>();
        services.AddSingleton<AccessTokenService>();
        services.AddSingleton<IMailSender>(_mails);
        services.AddScoped<TotpService>();
        services.AddScoped<UserService>();
        services.AddScoped<AuthService>();

        _services = services.BuildServiceProvider();

        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DataContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        // Fresh scope per step, like separate http requests
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private Task<AuthResult> RegisterAsync(string username, string email = "", string password = "password123")
        => RunAsync(p => p.GetRequiredService<AuthService>().RegisterAsync(
            new RegisterRequest { Username = username, Email = email == "" ? $"{username}@example.com" : email, Password = password },
            Client,
            CancellationToken.None));

    private Task<AuthResult> LoginAsync(string email, string password, string? code = null)
        => RunAsync(p => p.GetRequiredService<AuthService>().LoginAsync(
            new LoginRequest { Email = email, Password = password, TwoFactorCode = code },
            Client,
            CancellationToken.None));

    [Fact]
    public async Task FirstUserBecomesAdministrator()
    {
        var first = await RegisterAsync("alice");
        await RegisterAsync("bob");

        Assert.NotNull(first.Response.AccessToken);
        Assert.NotNull(first.RefreshToken);

        var permissions = await RunAsync(async p =>
        {
            var cache = p.GetRequiredService<UserAuthCache>();
            return ((await cache.GetAsync(1))!.Permissions, (await cache.GetAsync(2))!.Permissions);
        });

        Assert.Contains(Permissions.Everything, permissions.Item1);
        Assert.DoesNotContain(Permissions.Everything, permissions.Item2);
    }

    [Fact]
    public async Task RegistrationCanBeDisabledButNotForTheFirstUser()
    {
        _configuration.Authentication.EnableRegistration = false;

        await RegisterAsync("admin");

        var exception = await Assert.ThrowsAsync<HttpApiException>(() => RegisterAsync("second"));
        Assert.Equal(403, exception.StatusCode);
    }

    [Fact]
    public async Task DuplicateEmailIsRejected()
    {
        await RegisterAsync("alice", "alice@example.com");

        var exception = await Assert.ThrowsAsync<HttpApiException>(() => RegisterAsync("alice2", "ALICE@example.com"));
        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task LoginChecksPasswordAndLockState()
    {
        await RegisterAsync("alice");
        await RegisterAsync("bob");

        var wrongPassword = await Assert.ThrowsAsync<HttpApiException>(() => LoginAsync("bob@example.com", "wrong-password"));
        Assert.Equal(401, wrongPassword.StatusCode);

        var unknownUser = await Assert.ThrowsAsync<HttpApiException>(() => LoginAsync("nobody@example.com", "password123"));
        Assert.Equal(401, unknownUser.StatusCode);

        var login = await LoginAsync("Bob@Example.com", "password123");
        Assert.NotNull(login.Response.AccessToken);

        await RunAsync(async p =>
        {
            var users = p.GetRequiredService<UserService>();
            var bob = await users.GetAsync(2, CancellationToken.None);

            return await users.UpdateAsync(2, new()
            {
                Username = bob.Username,
                Email = bob.Email,
                IsLocked = true
            }, actingUserId: 1, CancellationToken.None);
        });

        var locked = await Assert.ThrowsAsync<HttpApiException>(() => LoginAsync("bob@example.com", "password123"));
        Assert.Equal(403, locked.StatusCode);

        // Locking also revoked the existing session
        await Assert.ThrowsAsync<HttpApiException>(() => RunAsync(p =>
            p.GetRequiredService<AuthService>().RefreshAsync(login.RefreshToken!, Client, CancellationToken.None)));
    }

    [Fact]
    public async Task RefreshRotatesTheToken()
    {
        var registration = await RegisterAsync("alice");

        var refreshed = await RunAsync(p =>
            p.GetRequiredService<AuthService>().RefreshAsync(registration.RefreshToken!, Client, CancellationToken.None));

        Assert.NotNull(refreshed.Response.AccessToken);
        Assert.NotEqual(registration.RefreshToken, refreshed.RefreshToken);

        // The old refresh token is no longer valid
        await Assert.ThrowsAsync<HttpApiException>(() => RunAsync(p =>
            p.GetRequiredService<AuthService>().RefreshAsync(registration.RefreshToken!, Client, CancellationToken.None)));
    }

    [Fact]
    public async Task LogoutRevokesTheSession()
    {
        var registration = await RegisterAsync("alice");

        await RunAsync(async p =>
        {
            await p.GetRequiredService<AuthService>().LogoutAsync(registration.RefreshToken!, CancellationToken.None);
            return true;
        });

        await Assert.ThrowsAsync<HttpApiException>(() => RunAsync(p =>
            p.GetRequiredService<AuthService>().RefreshAsync(registration.RefreshToken!, Client, CancellationToken.None)));
    }

    [Fact]
    public async Task PasswordResetChangesPasswordAndRevokesSessions()
    {
        var registration = await RegisterAsync("alice");

        await RunAsync(async p =>
        {
            await p.GetRequiredService<AuthService>().RequestPasswordResetAsync("alice@example.com", CancellationToken.None);
            return true;
        });

        var mail = Assert.Single(_mails.Sent);
        var token = Uri.UnescapeDataString(mail.Body.Split("token=")[1].Split('\n')[0].Trim());

        await RunAsync(async p =>
        {
            await p.GetRequiredService<AuthService>().ConfirmPasswordResetAsync(token, "new-password-1", CancellationToken.None);
            return true;
        });

        await Assert.ThrowsAsync<HttpApiException>(() => LoginAsync("alice@example.com", "password123"));
        Assert.NotNull((await LoginAsync("alice@example.com", "new-password-1")).Response.AccessToken);

        // Old session is gone and the reset link can't be used twice
        await Assert.ThrowsAsync<HttpApiException>(() => RunAsync(p =>
            p.GetRequiredService<AuthService>().RefreshAsync(registration.RefreshToken!, Client, CancellationToken.None)));

        await Assert.ThrowsAsync<HttpApiException>(() => RunAsync(async p =>
        {
            await p.GetRequiredService<AuthService>().ConfirmPasswordResetAsync(token, "another-password", CancellationToken.None);
            return true;
        }));
    }

    [Fact]
    public async Task PasswordResetForUnknownEmailSendsNothing()
    {
        await RunAsync(async p =>
        {
            await p.GetRequiredService<AuthService>().RequestPasswordResetAsync("nobody@example.com", CancellationToken.None);
            return true;
        });

        Assert.Empty(_mails.Sent);
    }

    [Fact]
    public async Task TwoFactorLoginRequiresValidCode()
    {
        await RegisterAsync("alice");

        var secret = await RunAsync(async p =>
        {
            var totp = p.GetRequiredService<TotpService>();
            var context = p.GetRequiredService<DataContext>();

            var plainSecret = totp.GenerateSecret();
            var user = await context.Users.SingleAsync();

            user.TotpSecret = totp.Protect(plainSecret);
            user.TotpEnabled = true;
            await context.SaveChangesAsync();

            return plainSecret;
        });

        var withoutCode = await LoginAsync("alice@example.com", "password123");
        Assert.True(withoutCode.Response.RequiresTwoFactor);
        Assert.Null(withoutCode.Response.AccessToken);
        Assert.Null(withoutCode.RefreshToken);

        await Assert.ThrowsAsync<HttpApiException>(() => LoginAsync("alice@example.com", "password123", "000000"));

        var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow);
        var withCode = await LoginAsync("alice@example.com", "password123", code);
        Assert.NotNull(withCode.Response.AccessToken);

        // The same code can't be replayed
        await Assert.ThrowsAsync<HttpApiException>(() => LoginAsync("alice@example.com", "password123", code));
    }

    private class CapturingMailSender : IMailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }
}
