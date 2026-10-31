using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Auth.Services;
using Midnight.Shared.Http.Auth;

namespace Midnight.ApiServer.Features.Auth.Http;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    public const string RefreshCookieName = "midnight_refresh";
    public const string RateLimitPolicy = "auth";

    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("settings")]
    public async Task<PublicSettingsResponse> GetSettings(
        [FromServices] DataContext context,
        [FromServices] IOptionsMonitor<AppConfiguration> configuration,
        CancellationToken cancellationToken)
    {
        var config = configuration.CurrentValue;
        var setupRequired = !await context.Users.AnyAsync(cancellationToken);

        return new PublicSettingsResponse
        {
            AppName = config.AppName,
            Version = typeof(AuthController).Assembly.GetName().Version?.ToString(3) ?? "",
            RegistrationEnabled = config.Authentication.EnableRegistration || setupRequired,
            SetupRequired = setupRequired,
            UserApiEnabled = config.Api.UserApiEnabled,
            MaintenanceMode = config.Features.MaintenanceMode,
            FooterLinks = config.Footer?.Links ?? []
        };
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<LoginResponse> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, GetClientInfo(), cancellationToken);
        return Complete(result);
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<LoginResponse> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, GetClientInfo(), cancellationToken);
        return Complete(result);
    }

    /// <summary>Issues a new access token using the refresh token cookie</summary>
    [HttpPost("refresh")]
    public async Task<LoginResponse> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
            throw HttpApiException.Unauthorized("Not logged in");

        try
        {
            var result = await _authService.RefreshAsync(refreshToken, GetClientInfo(), cancellationToken);
            return Complete(result);
        }
        catch (HttpApiException)
        {
            DeleteRefreshCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
            await _authService.LogoutAsync(refreshToken, cancellationToken);

        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("password-reset")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequest request, CancellationToken cancellationToken)
    {
        await _authService.RequestPasswordResetAsync(request.Email, cancellationToken);
        return Accepted();
    }

    [HttpPost("password-reset/confirm")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> ConfirmPasswordReset([FromBody] PasswordResetConfirmRequest request, CancellationToken cancellationToken)
    {
        await _authService.ConfirmPasswordResetAsync(request.Token, request.NewPassword, cancellationToken);
        return NoContent();
    }

    private LoginResponse Complete(AuthResult result)
    {
        if (result.RefreshToken != null)
        {
            Response.Cookies.Append(RefreshCookieName, result.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth",
                Expires = result.RefreshTokenExpiresAt,
                IsEssential = true
            });
        }

        return result.Response;
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth"
        });
    }

    private ClientInfo GetClientInfo()
        => new(HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
}
