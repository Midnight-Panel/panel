using System.Security.Claims;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Account.Services;
using Midnight.Shared;

namespace Midnight.Panel.Tests;

public class PasswordHasherTests
{
    // Cheap parameters so the tests stay fast
    private readonly PasswordHasher _hasher = new(memoryKb: 1024, iterations: 1, parallelism: 1);

    [Fact]
    public void Verify_AcceptsCorrectPassword()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.StartsWith("$argon2id$v=19$m=1024,t=1,p=1$", hash);
        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var hash = _hasher.Hash("secret-password");

        Assert.False(_hasher.Verify("secret-passwort", hash));
    }

    [Fact]
    public void Hash_UsesRandomSalt()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plaintext")]
    [InlineData("$argon2i$v=19$m=1024,t=1,p=1$c2FsdA==$aGFzaA==")]
    [InlineData("$argon2id$v=19$m=0,t=1,p=1$c2FsdA==$aGFzaA==")]
    [InlineData("$argon2id$v=19$m=1024,t=1,p=1$not-base64!$aGFzaA==")]
    public void Verify_RejectsMalformedHashes(string encoded)
    {
        Assert.False(_hasher.Verify("password", encoded));
    }

    [Fact]
    public void NeedsRehash_DetectsWeakerParameters()
    {
        var weak = _hasher.Hash("password");

        Assert.False(_hasher.NeedsRehash(weak));
        Assert.True(new PasswordHasher(memoryKb: 2048, iterations: 1, parallelism: 1).NeedsRehash(weak));
    }
}

public class SecureTokensTests
{
    [Fact]
    public void HashEquals_MatchesOnlySameToken()
    {
        var token = SecureTokens.Generate();
        var hash = SecureTokens.Hash(token);

        Assert.True(SecureTokens.HashEquals(token, hash));
        Assert.False(SecureTokens.HashEquals(token + "x", hash));
    }

    [Fact]
    public void GenerateAlphanumeric_OnlyUsesSafeCharacters()
    {
        var value = SecureTokens.GenerateAlphanumeric(200);

        Assert.Equal(200, value.Length);
        Assert.All(value, c => Assert.True(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)));
    }
}

public class ApiKeyFormatTests
{
    [Fact]
    public void TryParse_ExtractsPrefix()
    {
        Assert.True(ApiKeyAuthenticationHandler.TryParse("mdp_abc12345_secretpart", out var prefix));
        Assert.Equal("abc12345", prefix);
    }

    [Theory]
    [InlineData("mdp_abc")]
    [InlineData("mdp__secret")]
    [InlineData("mdp_abc_")]
    [InlineData("xyz_abc_secret")]
    [InlineData("mdp_a_b_c")]
    public void TryParse_RejectsInvalidKeys(string key)
    {
        Assert.False(ApiKeyAuthenticationHandler.TryParse(key, out _));
    }
}

public class ClaimsPermissionTests
{
    private static ClaimsPrincipal CreatePrincipal(string authType, string[] permissions, string[]? scopes = null, int? impersonatorId = null)
    {
        var claims = new List<Claim>
        {
            new(MidnightClaims.UserId, "1"),
            new(MidnightClaims.AuthType, authType)
        };

        claims.AddRange(permissions.Select(x => new Claim(MidnightClaims.Permission, x)));
        claims.AddRange((scopes ?? []).Select(x => new Claim(MidnightClaims.Scope, x)));

        if (impersonatorId.HasValue)
            claims.Add(new Claim(MidnightClaims.ImpersonatorId, impersonatorId.Value.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void Session_UsesUserPermissions()
    {
        var principal = CreatePrincipal(MidnightClaims.AuthTypeSession, ["admin.users.*", ..Permissions.UserDefaults]);

        Assert.True(principal.HasPermission(Permissions.AdminUsersWrite));
        Assert.True(principal.HasPermission(Permissions.AccountSecurity));
        Assert.False(principal.HasPermission(Permissions.AdminRolesWrite));
    }

    [Fact]
    public void ApiKey_NeedsPermissionAndScope()
    {
        var principal = CreatePrincipal(MidnightClaims.AuthTypeApiKey, ["admin.*"], ["admin.users.read"]);

        Assert.True(principal.HasPermission(Permissions.AdminUsersRead));
        Assert.False(principal.HasPermission(Permissions.AdminUsersWrite));
    }

    [Fact]
    public void ApiKey_ScopeCannotExceedUserPermissions()
    {
        var principal = CreatePrincipal(MidnightClaims.AuthTypeApiKey, ["account.read"], ["*"]);

        Assert.True(principal.HasPermission(Permissions.AccountRead));
        Assert.False(principal.HasPermission(Permissions.AdminUsersRead));
    }

    [Fact]
    public void ApiKey_NeverGetsSecurityActions()
    {
        var principal = CreatePrincipal(MidnightClaims.AuthTypeApiKey, ["*"], ["*"]);

        Assert.False(principal.HasPermission(Permissions.AccountSecurity));
    }

    [Fact]
    public void Impersonation_BlocksSecurityActions()
    {
        var principal = CreatePrincipal(MidnightClaims.AuthTypeSession, [..Permissions.UserDefaults], impersonatorId: 2);

        Assert.True(principal.HasPermission(Permissions.AccountWrite));
        Assert.False(principal.HasPermission(Permissions.AccountSecurity));
    }

    [Fact]
    public void Anonymous_HasNoPermissions()
    {
        Assert.False(new ClaimsPrincipal(new ClaimsIdentity()).HasPermission(Permissions.AccountRead));
    }
}

public class AvatarFormatTests
{
    [Fact]
    public void DetectExtension_RecognizesImages()
    {
        Assert.Equal(".png", AvatarService.DetectExtension([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]));
        Assert.Equal(".jpg", AvatarService.DetectExtension([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal(".webp", AvatarService.DetectExtension("RIFF\0\0\0\0WEBPVP8 "u8));
    }

    [Fact]
    public void DetectExtension_RejectsOtherContent()
    {
        Assert.Null(AvatarService.DetectExtension("<svg onload=alert(1)>"u8));
        Assert.Null(AvatarService.DetectExtension([]));
    }
}
