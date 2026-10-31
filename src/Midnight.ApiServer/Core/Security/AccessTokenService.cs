using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Midnight.ApiServer.Core.Configuration;

namespace Midnight.ApiServer.Core.Security;

public class AccessTokenService
{
    public const string Issuer = "midnight-panel";
    public const string Audience = "midnight-panel";

    private readonly IOptionsMonitor<AppConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;

    public AccessTokenService(IOptionsMonitor<AppConfiguration> configuration, TimeProvider timeProvider)
    {
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public (string Token, DateTimeOffset ExpiresAt) Create(int userId, string securityStamp, int? sessionId, int? impersonatorId, TimeSpan lifetime)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(lifetime);

        var claims = new Dictionary<string, object>
        {
            [MidnightClaims.UserId] = userId.ToString(),
            [MidnightClaims.SecurityStamp] = securityStamp,
            [MidnightClaims.AuthType] = MidnightClaims.AuthTypeSession
        };

        if (sessionId.HasValue)
            claims[MidnightClaims.SessionId] = sessionId.Value.ToString();

        if (impersonatorId.HasValue)
            claims[MidnightClaims.ImpersonatorId] = impersonatorId.Value.ToString();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(GetSigningKey(_configuration.CurrentValue), SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return (token, expiresAt);
    }

    public static SymmetricSecurityKey GetSigningKey(AppConfiguration configuration)
        => new(Convert.FromBase64String(configuration.Security.JwtSecret));

    public static TokenValidationParameters CreateValidationParameters(AppConfiguration configuration) => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = GetSigningKey(configuration),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = MidnightClaims.UserId,
        RoleClaimType = ClaimTypes.Role
    };
}
