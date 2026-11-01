using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Features.Nodes.Http;

namespace Midnight.ApiServer.Core.Security;

public static class AuthenticationExtensions
{
    public const string DefaultScheme = "Midnight";

    public static IServiceCollection AddMidnightAuthentication(this IServiceCollection services, AppConfiguration configuration)
    {
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<UserAuthCache>();
        services.AddSingleton<AccessTokenService>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, ApplicationAccessAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, SessionAuthorizationHandler>();

        services
            .AddAuthentication(DefaultScheme)
            // Selects the api key, application key or jwt handler depending on the token format
            .AddPolicyScheme(DefaultScheme, DefaultScheme, options =>
            {
                options.ForwardDefaultSelector = context => SelectScheme(ApiKeyAuthenticationHandler.GetBearerToken(context.Request));
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null)
            .AddScheme<AuthenticationSchemeOptions, ApplicationKeyAuthenticationHandler>(ApplicationKeyAuthenticationHandler.SchemeName, null)
            .AddScheme<AuthenticationSchemeOptions, NodeAuthenticationHandler>(NodeAuthenticationHandler.SchemeName, null)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = AccessTokenService.CreateValidationParameters(configuration);

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateUserAsync
                };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>The handler for a bearer token: mdp_ = personal api key, mda_ = application key, everything else = jwt</summary>
    public static string SelectScheme(string? token)
    {
        if (token != null && token.StartsWith(ApiKeyAuthenticationHandler.KeyPrefix, StringComparison.Ordinal))
            return ApiKeyAuthenticationHandler.SchemeName;

        if (token != null && token.StartsWith(ApplicationKeyAuthenticationHandler.KeyPrefix, StringComparison.Ordinal))
            return ApplicationKeyAuthenticationHandler.SchemeName;

        return JwtBearerDefaults.AuthenticationScheme;
    }

    /// <summary>
    /// Runs after the signature check: rejects tokens of locked users or with an outdated
    /// security stamp and attaches the current permissions of the user.
    /// </summary>
    private static async Task ValidateUserAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;

        if (principal?.Identity is not ClaimsIdentity identity ||
            !int.TryParse(principal.FindFirstValue(MidnightClaims.UserId), out var userId))
        {
            context.Fail("Invalid token");
            return;
        }

        var cache = context.HttpContext.RequestServices.GetRequiredService<UserAuthCache>();
        var user = await cache.GetAsync(userId, context.HttpContext.RequestAborted);

        if (user == null || user.IsLocked || user.SecurityStamp != principal.FindFirstValue(MidnightClaims.SecurityStamp))
        {
            context.Fail("Token is no longer valid");
            return;
        }

        foreach (var permission in user.Permissions)
            identity.AddClaim(new Claim(MidnightClaims.Permission, permission));
    }
}
