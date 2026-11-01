using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Mail;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Account.Services;
using Midnight.ApiServer.Features.Application.Entities;
using Midnight.ApiServer.Features.Application.Services;
using Midnight.ApiServer.Features.Auth.Services;
using Midnight.ApiServer.Features.Databases.Services;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.ApiServer.Features.Shards.Services;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.ApiServer.Features.Users.Services;
using Midnight.Shared;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Account;
using Midnight.Shared.Http.Admin;
using Midnight.Shared.Http.Application;
using Midnight.Shared.Http.Servers;

namespace Midnight.Panel.Tests;

public class IpRulesTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData(" 10.1.2.3/8 ", "10.0.0.0/8")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:10.0.0.0/104", "10.0.0.0/8")]
    [InlineData("2001:db8::1/32", "2001:db8::/32")]
    [InlineData("2001:DB8::1", "2001:db8::1")]
    public void TryNormalize_AcceptsAddressesAndRanges(string input, string expected)
    {
        Assert.True(IpRules.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.0/-1")]
    [InlineData("10.0.0.0/")]
    [InlineData("fe80::1%eth0")]
    [InlineData("1.2.3.4/8/8")]
    public void TryNormalize_RejectsInvalidValues(string input)
    {
        Assert.False(IpRules.TryNormalize(input, out _));
    }

    [Fact]
    public void IsAllowed_MatchesSingleAddressesRangesAndMappedClients()
    {
        string[] rules = ["203.0.113.7", "10.0.0.0/8", "2001:db8::/32"];

        Assert.True(IpRules.IsAllowed(IPAddress.Parse("203.0.113.7"), rules));
        Assert.True(IpRules.IsAllowed(IPAddress.Parse("::ffff:203.0.113.7"), rules));
        Assert.True(IpRules.IsAllowed(IPAddress.Parse("::ffff:10.20.30.40"), rules));
        Assert.True(IpRules.IsAllowed(IPAddress.Parse("2001:db8:1::5"), rules));
        Assert.False(IpRules.IsAllowed(IPAddress.Parse("203.0.113.8"), rules));
        Assert.False(IpRules.IsAllowed(IPAddress.Parse("2001:db9::1"), rules));
        Assert.False(IpRules.IsAllowed(null, rules));
    }

    [Fact]
    public void IsAllowed_EmptyListAllowsEverything()
    {
        Assert.True(IpRules.IsAllowed(null, []));
        Assert.True(IpRules.IsAllowed(IPAddress.Parse("198.51.100.1"), []));
    }
}

public class ApplicationKeyRulesTests
{
    [Fact]
    public void TryParse_ExtractsIdentifier()
    {
        Assert.True(ApplicationKeyAuthenticationHandler.TryParse("mda_abc12345_secretpart", out var identifier));
        Assert.Equal("abc12345", identifier);
    }

    [Theory]
    [InlineData("mda_abc")]
    [InlineData("mda__secret")]
    [InlineData("mdp_abc_secret")]
    [InlineData("mda_a_b_c")]
    public void TryParse_RejectsInvalidKeys(string key)
    {
        Assert.False(ApplicationKeyAuthenticationHandler.TryParse(key, out _));
    }

    [Theory]
    [InlineData("mda_abc_def", ApplicationKeyAuthenticationHandler.SchemeName)]
    [InlineData("mdp_abc_def", ApiKeyAuthenticationHandler.SchemeName)]
    [InlineData("eyJhbGciOi.x.y", "Bearer")]
    [InlineData(null, "Bearer")]
    public void SelectScheme_UsesTokenPrefix(string? token, string scheme)
    {
        Assert.Equal(scheme, AuthenticationExtensions.SelectScheme(token));
    }

    [Theory]
    [InlineData(ApplicationResource.Users, ApplicationAccess.Read, new[] { Permissions.AdminUsersRead })]
    [InlineData(ApplicationResource.Users, ApplicationAccess.ReadWrite, new[] { Permissions.AdminUsersRead, Permissions.AdminUsersWrite })]
    [InlineData(ApplicationResource.Servers, ApplicationAccess.ReadWrite, new[] { Permissions.AdminServersRead, Permissions.AdminServersWrite })]
    [InlineData(ApplicationResource.Nodes, ApplicationAccess.Read, new[] { Permissions.AdminNodesRead })]
    [InlineData(ApplicationResource.Allocations, ApplicationAccess.ReadWrite, new[] { Permissions.AdminNodesRead, Permissions.AdminNodesWrite })]
    [InlineData(ApplicationResource.Shards, ApplicationAccess.Read, new[] { Permissions.AdminShardsRead })]
    [InlineData(ApplicationResource.DatabaseHosts, ApplicationAccess.ReadWrite, new[] { Permissions.AdminServersRead, Permissions.AdminServersWrite })]
    [InlineData(ApplicationResource.Users, ApplicationAccess.None, new string[0])]
    public void GrantRule_MapsResourcesToAdminPermissions(ApplicationResource resource, ApplicationAccess access, string[] expected)
    {
        Assert.Equal(expected, ApplicationKeyService.GetRequiredPermissions(resource, access));
    }

    [Fact]
    public void GrantRule_RejectsAccessTheAdminDoesNotHave()
    {
        string[] admin = [Permissions.AdminServersRead, Permissions.AdminServersWrite, Permissions.AdminUsersRead];

        ApplicationKeyService.EnsureCanGrant(admin, Request((ApplicationResource.Servers, ApplicationAccess.ReadWrite), (ApplicationResource.Users, ApplicationAccess.Read)), null);

        var error = Assert.Throws<HttpApiException>(() =>
            ApplicationKeyService.EnsureCanGrant(admin, Request((ApplicationResource.Users, ApplicationAccess.ReadWrite)), null));

        Assert.Equal(403, error.StatusCode);
        Assert.StartsWith("You can't grant", error.Title);
        Assert.Contains("Permissions.Users", error.Errors!.Keys);

        Assert.Throws<HttpApiException>(() => ApplicationKeyService.EnsureCanGrant(admin, Request((ApplicationResource.Nodes, ApplicationAccess.Read)), null));
    }

    [Fact]
    public void GrantRule_WildcardsCount()
    {
        ApplicationKeyService.EnsureCanGrant(["admin.*"], Request(Enum.GetValues<ApplicationResource>().Select(x => (x, ApplicationAccess.ReadWrite)).ToArray()), null);
        ApplicationKeyService.EnsureCanGrant([Permissions.Everything], Request((ApplicationResource.Shards, ApplicationAccess.ReadWrite)), null);
    }

    [Fact]
    public void GrantRule_OnUpdateOnlyRaisedLevelsAreChecked()
    {
        var current = Request((ApplicationResource.Users, ApplicationAccess.ReadWrite), (ApplicationResource.Servers, ApplicationAccess.Read));

        // Keeping or lowering is fine without the permissions
        ApplicationKeyService.EnsureCanGrant([], Request((ApplicationResource.Users, ApplicationAccess.Read), (ApplicationResource.Servers, ApplicationAccess.Read)), current);

        // Raising is not
        Assert.Throws<HttpApiException>(() =>
            ApplicationKeyService.EnsureCanGrant([Permissions.AdminServersRead], Request((ApplicationResource.Servers, ApplicationAccess.ReadWrite)), current));
    }

    [Fact]
    public void Validate_NormalizesAndFillsEveryResource()
    {
        var (description, permissions, ips) = ApplicationKeyService.Validate(new ApplicationKeyRequest
        {
            Description = "  WHMCS  ",
            Permissions = new Dictionary<ApplicationResource, ApplicationAccess> { [ApplicationResource.Servers] = ApplicationAccess.ReadWrite },
            AllowedIps = ["10.1.2.3/8", "10.0.0.0/8", "::ffff:203.0.113.9", " "]
        });

        Assert.Equal("WHMCS", description);
        Assert.Equal(Enum.GetValues<ApplicationResource>().Length, permissions.Count);
        Assert.Equal(ApplicationAccess.ReadWrite, permissions[ApplicationResource.Servers]);
        Assert.Equal(ApplicationAccess.None, permissions[ApplicationResource.Users]);
        Assert.Equal(["10.0.0.0/8", "203.0.113.9"], ips);
    }

    [Fact]
    public void Validate_ReportsFieldErrors()
    {
        var error = Assert.Throws<HttpApiException>(() => ApplicationKeyService.Validate(new ApplicationKeyRequest
        {
            Description = " ",
            Permissions = new Dictionary<ApplicationResource, ApplicationAccess> { [ApplicationResource.Users] = (ApplicationAccess)7 },
            AllowedIps = ["not-an-ip"]
        }));

        Assert.Equal(400, error.StatusCode);
        Assert.Contains("Description", error.Errors!.Keys);
        Assert.Contains("AllowedIps", error.Errors.Keys);
        Assert.Contains("Permissions.Users", error.Errors.Keys);
    }

    [Fact]
    public void Permissions_RoundTripAsNames()
    {
        var key = new ApplicationKey();
        key.SetPermissions(Request((ApplicationResource.DatabaseHosts, ApplicationAccess.Read), (ApplicationResource.Servers, ApplicationAccess.ReadWrite)));

        Assert.Equal("{\"Servers\":\"ReadWrite\",\"DatabaseHosts\":\"Read\"}", key.PermissionsJson);
        Assert.Equal(ApplicationAccess.ReadWrite, key.GetPermissions()[ApplicationResource.Servers]);
        Assert.Equal(ApplicationAccess.None, key.GetPermissions()[ApplicationResource.Nodes]);

        key.PermissionsJson = "{\"Servers\":\"Bogus\",\"Unknown\":\"Read\"";
        Assert.All(key.GetPermissions().Values, x => Assert.Equal(ApplicationAccess.None, x));
    }

    [Fact]
    public void Claims_GiveAccessOnlyToApplicationKeys()
    {
        var key = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(MidnightClaims.AuthType, MidnightClaims.AuthTypeApplicationKey),
            new Claim(MidnightClaims.ApplicationAccess, "Servers:Read"),
            new Claim(MidnightClaims.UserId, "1"),
            new Claim(MidnightClaims.Permission, "*")
        ], "test"));

        Assert.True(key.HasApplicationAccess(ApplicationResource.Servers, ApplicationAccess.Read));
        Assert.False(key.HasApplicationAccess(ApplicationResource.Servers, ApplicationAccess.ReadWrite));
        Assert.False(key.HasApplicationAccess(ApplicationResource.Users, ApplicationAccess.Read));

        // Never usable for permission checks of the panel api, even with a forged permission claim
        Assert.False(key.HasPermission(Permissions.AccountRead));
        Assert.False(key.IsRegularSession());

        var session = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(MidnightClaims.AuthType, MidnightClaims.AuthTypeSession),
            new Claim(MidnightClaims.ApplicationAccess, "Servers:ReadWrite"),
            new Claim(MidnightClaims.UserId, "1")
        ], "test"));

        Assert.False(session.HasApplicationAccess(ApplicationResource.Servers, ApplicationAccess.Read));
        Assert.True(session.IsRegularSession());
    }

    [Fact]
    public void RateLimit_240PerMinuteAndKey()
    {
        Assert.Equal(240, ApplicationRateLimit.DefaultPermitLimit);
        Assert.Equal(ApplicationRateLimit.DefaultPermitLimit, new AppConfiguration().Api.ApplicationRateLimitPerMinute);

        var first = ContextForKey(1);
        var second = ContextForKey(2);

        Assert.Equal("key:1", ApplicationRateLimit.GetPartitionKey(first));
        Assert.Equal("key:2", ApplicationRateLimit.GetPartitionKey(second));

        var anonymous = new DefaultHttpContext();
        anonymous.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.4");
        Assert.Equal("ip:198.51.100.4", ApplicationRateLimit.GetPartitionKey(anonymous));

        var partition = ApplicationRateLimit.GetPartition(first, ApplicationRateLimit.DefaultPermitLimit);
        using var limiter = partition.Factory(partition.PartitionKey);

        for (var i = 0; i < 240; i++)
            Assert.True(limiter.AttemptAcquire().IsAcquired);

        Assert.False(limiter.AttemptAcquire().IsAcquired);
    }

    private static DefaultHttpContext ContextForKey(int id) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(MidnightClaims.AuthType, MidnightClaims.AuthTypeApplicationKey),
            new Claim(MidnightClaims.ApplicationKeyId, id.ToString())
        ], "test"))
    };

    private static Dictionary<ApplicationResource, ApplicationAccess> Request(params (ApplicationResource Resource, ApplicationAccess Access)[] entries)
        => entries.ToDictionary(x => x.Resource, x => x.Access);
}

/// <summary>
/// The real authentication, authorization, rate limiting and the controllers of the api server in a test server,
/// with an in-memory sqlite database and a demon that accepts everything
/// </summary>
public sealed class ApplicationApiHttpTests : IAsyncLifetime
{
    private const string Base = "/api/application/v1";
    private const int RateLimit = 30;

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AcceptingDemon _demon = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private int _adminId;
    private string _adminStamp = "";
    private int _limitedAdminId;
    private int _nodeId;
    private int _otherNodeId;
    private int _shardId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();

        var configuration = new AppConfiguration();
        configuration.Security.JwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        configuration.Api.ApplicationRateLimitPerMinute = RateLimit;

        var services = builder.Services;
        services.AddOptions<AppConfiguration>().Configure(options =>
        {
            options.Security.JwtSecret = configuration.Security.JwtSecret;
            options.Api.ApplicationRateLimitPerMinute = RateLimit;
        });

        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<SqliteDataContext>(options => options.UseSqlite(_connection));
        services.AddScoped<DataContext>(provider => provider.GetRequiredService<SqliteDataContext>());

        services.AddMidnightAuthentication(configuration);
        services.AddSingleton(new PasswordHasher(1024, 1, 1));
        services.AddSingleton<IMailSender, LogMailSender>();
        services.AddSingleton<IHttpClientFactory>(_demon);

        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddControllers()
            .AddApplicationPart(typeof(ApplicationKeyService).Assembly)
            .AddJsonOptions(options => JsonDefaults.Apply(options.JsonSerializerOptions));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddApplicationPolicy(RateLimit);
            options.OnRejected = (context, _) =>
            {
                ApplicationRateLimit.SetRetryAfter(context);
                return ValueTask.CompletedTask;
            };
        });

        services.AddScoped<AuthService>();
        services.AddScoped<TotpService>();
        services.AddScoped<UserService>();
        services.AddScoped<ApiKeyService>();
        services.AddScoped<ApplicationKeyService>();
        services.AddScoped<NodeService>();
        services.AddScoped<NodeClient>();
        services.AddScoped<AllocationService>();
        services.AddScoped<ServerService>();
        services.AddScoped<ActivityService>();
        services.AddSingleton<IDatabaseExecutor, FakeDatabaseExecutor>();
        services.AddScoped<DatabaseHostService>();
        services.AddScoped<ServerDatabaseService>();
        services.AddScoped<IShardService, ShardService>();
        services.AddScoped<IShardCollectionService, ShardCollectionService>();

        _app = builder.Build();

        // Stands in for the client address after UseForwardedHeaders
        _app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Ip", out var ip))
                context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());

            return next(context);
        });

        _app.UseExceptionHandler();
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseRateLimiter();
        _app.UseAuthorization();
        _app.MapControllers();

        await _app.StartAsync();
        _client = _app.GetTestClient();

        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task SeedAsync()
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var adminRole = new Role { Name = UserService.AdministratorRoleName, Permissions = [Permissions.Everything] };
        var limitedRole = new Role { Name = "Api managers", Permissions = [Permissions.AdminApiRead, Permissions.AdminApiWrite, Permissions.AdminServersRead, Permissions.AdminUsersRead, Permissions.AdminUsersWrite] };

        var admin = new User { Username = "admin", Email = "admin@example.com", SecurityStamp = "stamp-admin", CreatedAt = now, Roles = [adminRole] };
        var limited = new User { Username = "limited", Email = "limited@example.com", SecurityStamp = "stamp-limited", CreatedAt = now, Roles = [limitedRole] };
        context.Users.AddRange(admin, limited);
        await context.SaveChangesAsync();

        var nodes = scope.ServiceProvider.GetRequiredService<NodeService>();
        _nodeId = (await nodes.CreateAsync(new Shared.Http.Nodes.NodeRequest { Name = "node-1", Fqdn = "node1.example.com" }, CancellationToken.None)).Id;
        _otherNodeId = (await nodes.CreateAsync(new Shared.Http.Nodes.NodeRequest { Name = "node-2", Fqdn = "node2.example.com" }, CancellationToken.None)).Id;

        context.Allocations.AddRange(Enumerable.Range(25565, 6).Select(port => new Allocation { NodeId = _nodeId, IpAddress = "10.0.0.1", Port = port }));
        context.Allocations.AddRange(Enumerable.Range(27015, 3).Select(port => new Allocation { NodeId = _otherNodeId, IpAddress = "10.0.0.2", Port = port }));

        var shard = new Shard
        {
            Collection = new ShardCollection { Name = "Minecraft", CreatedAt = now, UpdatedAt = now },
            Name = "Paper",
            DefaultDockerImage = "ghcr.io/java:21",
            StartupCommand = "java -jar {{SERVER_JARFILE}}",
            CreatedAt = now,
            UpdatedAt = now,
            Variables = [new ShardVariable { Name = "Jar", EnvVariable = "SERVER_JARFILE", DefaultValue = "server.jar", Rules = "required|string" }]
        };
        context.Shards.Add(shard);
        await context.SaveChangesAsync();

        _adminId = admin.Id;
        _adminStamp = admin.SecurityStamp;
        _limitedAdminId = limited.Id;
        _shardId = shard.Id;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private Task<CreatedApplicationKeyResponse> CreateKeyAsync(Dictionary<ApplicationResource, ApplicationAccess> permissions, List<string>? allowedIps = null, int? creatorId = null)
        => WithScopeAsync(p => p.GetRequiredService<ApplicationKeyService>().CreateAsync(creatorId ?? _adminId, [Permissions.Everything], new ApplicationKeyRequest
        {
            Description = "Billing",
            Permissions = permissions,
            AllowedIps = allowedIps ?? []
        }, CancellationToken.None));

    private async Task<string> CreateTokenAsync(ApplicationResource resource, ApplicationAccess access)
        => (await CreateKeyAsync(new Dictionary<ApplicationResource, ApplicationAccess> { [resource] = access })).Token;

    private async Task<string> FullAccessTokenAsync(int? creatorId = null)
        => (await CreateKeyAsync(Enum.GetValues<ApplicationResource>().ToDictionary(x => x, _ => ApplicationAccess.ReadWrite), creatorId: creatorId)).Token;

    private string SessionToken(int? userId = null, string? stamp = null, int? impersonatorId = null)
        => _app.Services.GetRequiredService<AccessTokenService>()
            .Create(userId ?? _adminId, stamp ?? _adminStamp, null, impersonatorId, TimeSpan.FromMinutes(5)).Token;

    private Task<string> PersonalApiKeyAsync()
        => WithScopeAsync(async p => (await p.GetRequiredService<ApiKeyService>()
            .CreateAsync(_adminId, [Permissions.Everything, ..Permissions.UserDefaults], new CreateApiKeyRequest { Name = "cli", Scopes = ["*"] }, CancellationToken.None)).Key);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null, string? ip = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (ip != null)
            request.Headers.Add("X-Test-Ip", ip);

        if (body != null)
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonDefaults.Options);

        return await _client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options))!;

    private ApplicationCreateServerRequest ServerRequest(string? externalId = null, ApplicationDeployOptions? deploy = null) => new()
    {
        Name = "Survival",
        OwnerId = _adminId,
        ShardId = _shardId,
        Limits = new ServerLimitsDto { Memory = 1024 },
        ExternalId = externalId,
        Deploy = deploy ?? new ApplicationDeployOptions { Ports = ["25567-25569"] }
    };

    // ─── Authentication ─────────────────────────────────────────────────────

    [Fact]
    public async Task ValidKey_IsAcceptedAndUpdatesLastUsed()
    {
        var created = await CreateKeyAsync(new Dictionary<ApplicationResource, ApplicationAccess> { [ApplicationResource.Users] = ApplicationAccess.Read });

        var response = await SendAsync(HttpMethod.Get, $"{Base}/users", created.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await ReadAsync<PagedResult<AdminUserResponse>>(response)).TotalCount);

        var key = await WithScopeAsync(p => p.GetRequiredService<DataContext>().ApplicationKeys.AsNoTracking().SingleAsync(x => x.Id == created.Key.Id));
        Assert.NotNull(key.LastUsedAt);
        Assert.NotEqual(created.Token, key.KeyHash);
        Assert.StartsWith("mda_", created.Key.Identifier);
        Assert.StartsWith(created.Key.Identifier + "_", created.Token);
    }

    [Fact]
    public async Task WrongSecret_Is401()
    {
        var token = await CreateTokenAsync(ApplicationResource.Users, ApplicationAccess.Read);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, $"{Base}/users", token[..^1] + (token[^1] == 'a' ? 'b' : 'a'))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, $"{Base}/users", "mda_unknown_secret")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, $"{Base}/users", "mda_malformed")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, $"{Base}/users", null)).StatusCode);
    }

    [Fact]
    public async Task RevokedKey_Is401()
    {
        var created = await CreateKeyAsync(new Dictionary<ApplicationResource, ApplicationAccess> { [ApplicationResource.Users] = ApplicationAccess.Read });
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"{Base}/users", created.Token)).StatusCode);

        var delete = await SendAsync(HttpMethod.Delete, $"/api/admin/application-keys/{created.Key.Id}", SessionToken());
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, $"{Base}/users", created.Token)).StatusCode);
    }

    [Fact]
    public async Task AllowedIps_AreEnforced()
    {
        var created = await CreateKeyAsync(new Dictionary<ApplicationResource, ApplicationAccess> { [ApplicationResource.Users] = ApplicationAccess.Read },
            ["203.0.113.0/24", "2001:db8::1"]);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"{Base}/users", created.Token, ip: "203.0.113.50")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"{Base}/users", created.Token, ip: "::ffff:203.0.113.51")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"{Base}/users", created.Token, ip: "2001:db8::1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, $"{Base}/users", created.Token, ip: "198.51.100.1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, $"{Base}/users", created.Token)).StatusCode);
    }

    [Fact]
    public async Task OtherTokens_AreRejectedOnApplicationRoutes()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, $"{Base}/users", SessionToken())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, $"{Base}/users", await PersonalApiKeyAsync())).StatusCode);
    }

    [Fact]
    public async Task ApplicationKeys_AreRejectedOutsideTheApplicationApi()
    {
        var token = await FullAccessTokenAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/admin/users", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/admin/servers", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/admin/application-keys", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/account", token)).StatusCode);

        // The same session token works there
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, "/api/admin/users", SessionToken())).StatusCode);
    }

    // ─── Access matrix ──────────────────────────────────────────────────────

    public static TheoryData<ApplicationResource, ApplicationAccess> Matrix()
    {
        var data = new TheoryData<ApplicationResource, ApplicationAccess>();

        foreach (var resource in Enum.GetValues<ApplicationResource>())
        foreach (var access in Enum.GetValues<ApplicationAccess>())
            data.Add(resource, access);

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task AccessMatrix(ApplicationResource resource, ApplicationAccess access)
    {
        var token = await CreateTokenAsync(resource, access);

        var read = resource switch
        {
            ApplicationResource.Users => $"{Base}/users",
            ApplicationResource.Servers => $"{Base}/servers",
            ApplicationResource.Nodes => $"{Base}/nodes",
            ApplicationResource.Allocations => $"{Base}/nodes/{_nodeId}/allocations",
            ApplicationResource.Shards => $"{Base}/shards",
            ApplicationResource.DatabaseHosts => $"{Base}/database-hosts",
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };

        // Writes on entries that don't exist: 404 once authorized
        string? write = resource switch
        {
            ApplicationResource.Users => $"{Base}/users/999999",
            ApplicationResource.Servers => $"{Base}/servers/999999",
            ApplicationResource.Nodes => $"{Base}/nodes/999999",
            ApplicationResource.Allocations => $"{Base}/nodes/{_nodeId}/allocations/999999",
            _ => null
        };

        var readStatus = (await SendAsync(HttpMethod.Get, read, token)).StatusCode;
        Assert.Equal(access >= ApplicationAccess.Read ? HttpStatusCode.OK : HttpStatusCode.Forbidden, readStatus);

        if (write != null)
        {
            var writeStatus = (await SendAsync(HttpMethod.Delete, write, token)).StatusCode;
            Assert.Equal(access == ApplicationAccess.ReadWrite ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, writeStatus);
        }

        // Access to one resource never opens another one
        var other = resource == ApplicationResource.Users ? $"{Base}/servers" : $"{Base}/users";
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, other, token)).StatusCode);
    }

    // ─── Key management ─────────────────────────────────────────────────────

    [Fact]
    public async Task KeyManagement_NeedsARegularSession()
    {
        var request = new ApplicationKeyRequest
        {
            Description = "WHMCS",
            Permissions = new Dictionary<ApplicationResource, ApplicationAccess> { [ApplicationResource.Servers] = ApplicationAccess.ReadWrite }
        };

        var created = await SendAsync(HttpMethod.Post, "/api/admin/application-keys", SessionToken(), request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var body = await ReadAsync<CreatedApplicationKeyResponse>(created);
        Assert.Equal(ApplicationAccess.ReadWrite, body.Key.Permissions[ApplicationResource.Servers]);
        Assert.Equal(ApplicationAccess.None, body.Key.Permissions[ApplicationResource.Users]);
        Assert.Equal("admin", body.Key.CreatedByUsername);

        // Personal api keys (even with "*" scope), impersonation and application keys can't manage keys
        var personal = await PersonalApiKeyAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, "/api/admin/application-keys", personal, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, "/api/admin/application-keys", personal)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Delete, $"/api/admin/application-keys/{body.Key.Id}", personal)).StatusCode);

        var impersonating = SessionToken(impersonatorId: _limitedAdminId);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, "/api/admin/application-keys", impersonating, request)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Post, "/api/admin/application-keys", body.Token, request)).StatusCode);

        var list = await ReadAsync<List<ApplicationKeyResponse>>(await SendAsync(HttpMethod.Get, "/api/admin/application-keys", SessionToken()));
        Assert.Contains(list, x => x.Id == body.Key.Id);
    }

    [Fact]
    public async Task KeyManagement_GrantRule()
    {
        var limited = SessionToken(_limitedAdminId, "stamp-limited");

        var allowed = await SendAsync(HttpMethod.Post, "/api/admin/application-keys", limited, new ApplicationKeyRequest
        {
            Description = "ok",
            Permissions = new Dictionary<ApplicationResource, ApplicationAccess>
            {
                [ApplicationResource.Servers] = ApplicationAccess.Read,
                [ApplicationResource.Users] = ApplicationAccess.ReadWrite
            }
        });
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        var allowedKey = (await ReadAsync<CreatedApplicationKeyResponse>(allowed)).Key;

        var denied = await SendAsync(HttpMethod.Post, "/api/admin/application-keys", limited, new ApplicationKeyRequest
        {
            Description = "too much",
            Permissions = new Dictionary<ApplicationResource, ApplicationAccess> { [ApplicationResource.Servers] = ApplicationAccess.ReadWrite }
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("You can't grant", await denied.Content.ReadAsStringAsync());

        // Raising an existing key is checked as well
        var raise = await SendAsync(HttpMethod.Put, $"/api/admin/application-keys/{allowedKey.Id}", limited, new ApplicationKeyRequest
        {
            Description = "ok",
            Permissions = new Dictionary<ApplicationResource, ApplicationAccess> { [ApplicationResource.Nodes] = ApplicationAccess.Read }
        });
        Assert.Equal(HttpStatusCode.Forbidden, raise.StatusCode);

        var invalidIp = await SendAsync(HttpMethod.Post, "/api/admin/application-keys", limited, new ApplicationKeyRequest
        {
            Description = "ip",
            AllowedIps = ["300.1.1.1"]
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidIp.StatusCode);
        Assert.Contains("allowedIps", (await invalidIp.Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
    }

    // ─── Rate limit ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RateLimit_PerKeyWithRetryAfter()
    {
        var first = await CreateTokenAsync(ApplicationResource.Shards, ApplicationAccess.Read);
        var second = await CreateTokenAsync(ApplicationResource.Shards, ApplicationAccess.Read);

        for (var i = 0; i < RateLimit; i++)
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"{Base}/shards", first)).StatusCode);

        var limited = await SendAsync(HttpMethod.Get, $"{Base}/shards", first);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"{Base}/shards", second)).StatusCode);
    }

    // ─── Users ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Users_CreateWithRandomPasswordAndExternalId()
    {
        var token = await FullAccessTokenAsync();

        var created = await SendAsync(HttpMethod.Post, $"{Base}/users", token, new ApplicationCreateUserRequest
        {
            Username = "customer1",
            Email = "Customer1@Example.com",
            ExternalId = "whmcs-client-17"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var user = await ReadAsync<ApplicationCreatedUserResponse>(created);
        Assert.True(user.PasswordGenerated);
        Assert.Equal("customer1@example.com", user.Email);
        Assert.Equal("whmcs-client-17", user.ExternalId);

        var hash = await WithScopeAsync(p => p.GetRequiredService<DataContext>().Users.Where(x => x.Id == user.Id).Select(x => x.PasswordHash).SingleAsync());
        Assert.StartsWith("$argon2id$", hash);

        var withPassword = await ReadAsync<ApplicationCreatedUserResponse>(await SendAsync(HttpMethod.Post, $"{Base}/users", token, new ApplicationCreateUserRequest
        {
            Username = "customer2",
            Email = "customer2@example.com",
            Password = "correct-horse-battery"
        }));
        Assert.False(withPassword.PasswordGenerated);
        var hasher = _app.Services.GetRequiredService<PasswordHasher>();
        var secondHash = await WithScopeAsync(p => p.GetRequiredService<DataContext>().Users.Where(x => x.Id == withPassword.Id).Select(x => x.PasswordHash).SingleAsync());
        Assert.True(hasher.Verify("correct-horse-battery", secondHash));

        // Lookups
        var byExternal = await SendAsync(HttpMethod.Get, $"{Base}/users/external/whmcs-client-17", token);
        Assert.Equal(user.Id, (await ReadAsync<AdminUserResponse>(byExternal)).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"{Base}/users/external/unknown", token)).StatusCode);

        var filtered = await ReadAsync<PagedResult<AdminUserResponse>>(await SendAsync(HttpMethod.Get, $"{Base}/users?filter[externalId]=whmcs-client-17", token));
        Assert.Equal(user.Id, Assert.Single(filtered.Items).Id);

        var byEmail = await ReadAsync<PagedResult<AdminUserResponse>>(await SendAsync(HttpMethod.Get, $"{Base}/users?filter[email]=CUSTOMER2@example.com", token));
        Assert.Equal(withPassword.Id, Assert.Single(byEmail.Items).Id);

        // Unique external id
        var duplicate = await SendAsync(HttpMethod.Post, $"{Base}/users", token, new ApplicationCreateUserRequest
        {
            Username = "customer3",
            Email = "customer3@example.com",
            ExternalId = "whmcs-client-17"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var patchDuplicate = await SendAsync(HttpMethod.Patch, $"{Base}/users/{withPassword.Id}", token, new ApplicationUpdateUserRequest { ExternalId = "whmcs-client-17" });
        Assert.Equal(HttpStatusCode.Conflict, patchDuplicate.StatusCode);

        // Partial update keeps the other values, empty external id removes it
        var patched = await ReadAsync<AdminUserResponse>(await SendAsync(HttpMethod.Patch, $"{Base}/users/{user.Id}", token, new ApplicationUpdateUserRequest { ExternalId = "", IsLocked = true }));
        Assert.Null(patched.ExternalId);
        Assert.True(patched.IsLocked);
        Assert.Equal("customer1", patched.Username);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"{Base}/users/{user.Id}", token)).StatusCode);
    }

    [Fact]
    public async Task Users_KeyCanNotExceedItsCreator()
    {
        // Creator with admin.users.* but no admin.roles.write: no role changes, no changes to the administrator
        var token = await FullAccessTokenAsync(_limitedAdminId);
        var roleId = await WithScopeAsync(p => p.GetRequiredService<DataContext>().Roles.Where(x => x.Name == UserService.AdministratorRoleName).Select(x => x.Id).SingleAsync());

        var withRole = await SendAsync(HttpMethod.Post, $"{Base}/users", token, new ApplicationCreateUserRequest
        {
            Username = "sneaky",
            Email = "sneaky@example.com",
            RoleIds = [roleId]
        });
        Assert.Equal(HttpStatusCode.Forbidden, withRole.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Patch, $"{Base}/users/{_adminId}", token, new ApplicationUpdateUserRequest { Password = "taken-over-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Delete, $"{Base}/users/{_adminId}", token)).StatusCode);

        // A key of the administrator can assign the role
        var adminToken = await FullAccessTokenAsync();
        var created = await SendAsync(HttpMethod.Post, $"{Base}/users", adminToken, new ApplicationCreateUserRequest
        {
            Username = "second-admin",
            Email = "second-admin@example.com",
            RoleIds = [roleId]
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Users_DeleteWithServersIsConflict()
    {
        var token = await FullAccessTokenAsync();
        var user = await ReadAsync<ApplicationCreatedUserResponse>(await SendAsync(HttpMethod.Post, $"{Base}/users", token, new ApplicationCreateUserRequest
        {
            Username = "owner",
            Email = "owner@example.com"
        }));

        var request = ServerRequest();
        request.OwnerId = user.Id;
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(HttpMethod.Post, $"{Base}/servers", token, request)).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(HttpMethod.Delete, $"{Base}/users/{user.Id}", token)).StatusCode);
    }

    // ─── Servers ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Servers_DeployPicksAFreeAllocationInThePortRange()
    {
        var token = await FullAccessTokenAsync();

        var first = await SendAsync(HttpMethod.Post, $"{Base}/servers", token, ServerRequest(deploy: new ApplicationDeployOptions
        {
            Ports = ["25567-25569"],
            AdditionalAllocations = 1
        }));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var server = await ReadAsync<AdminServerDetailResponse>(first);
        Assert.Equal(_nodeId, server.NodeId);
        Assert.Equal("10.0.0.1:25567", server.PrimaryAllocation);
        Assert.Equal([25567, 25568], server.Allocations.Select(x => x.Port).Order());
        Assert.Contains(_demon.Requests, x => x == "POST /api/servers");

        // 25567 and 25568 are taken now: the next one gets 25569
        var second = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Post, $"{Base}/servers", token, ServerRequest()));
        Assert.Equal("10.0.0.1:25569", second.PrimaryAllocation);

        // Range exhausted
        var none = await SendAsync(HttpMethod.Post, $"{Base}/servers", token, ServerRequest());
        Assert.Equal(HttpStatusCode.Conflict, none.StatusCode);

        // Only the given node, any port
        var onOtherNode = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Post, $"{Base}/servers", token,
            ServerRequest(deploy: new ApplicationDeployOptions { NodeId = _otherNodeId })));
        Assert.Equal(_otherNodeId, onOtherNode.NodeId);
        Assert.Equal("10.0.0.2:27015", onOtherNode.PrimaryAllocation);

        // Port list on any node: only node 2 has 27016
        var anyNode = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Post, $"{Base}/servers", token,
            ServerRequest(deploy: new ApplicationDeployOptions { Ports = ["27016", "30000-30010"] })));
        Assert.Equal("10.0.0.2:27016", anyNode.PrimaryAllocation);

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, $"{Base}/servers", token,
            ServerRequest(deploy: new ApplicationDeployOptions { Ports = ["70000"] }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, $"{Base}/servers", token,
            ServerRequest(deploy: new ApplicationDeployOptions { NodeIds = [999999] }))).StatusCode);
    }

    [Fact]
    public async Task Servers_ExplicitAllocationNeedsNode()
    {
        var token = await FullAccessTokenAsync();
        var allocationId = await WithScopeAsync(p => p.GetRequiredService<DataContext>().Allocations.Where(x => x.NodeId == _nodeId && x.Port == 25570).Select(x => x.Id).SingleAsync());

        var request = ServerRequest();
        request.Deploy = null;
        request.PrimaryAllocationId = allocationId;

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, $"{Base}/servers", token, request)).StatusCode);

        request.NodeId = _nodeId;
        var created = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Post, $"{Base}/servers", token, request));
        Assert.Equal("10.0.0.1:25570", created.PrimaryAllocation);

        var both = ServerRequest();
        both.PrimaryAllocationId = allocationId;
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, $"{Base}/servers", token, both)).StatusCode);
    }

    [Fact]
    public async Task Servers_ExternalIdLookupsAndUniqueness()
    {
        var token = await FullAccessTokenAsync();

        var created = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Post, $"{Base}/servers", token, ServerRequest("service-42")));
        Assert.Equal("service-42", created.ExternalId);

        var byExternal = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Get, $"{Base}/servers/external/service-42", token));
        Assert.Equal(created.Id, byExternal.Id);

        var filtered = await ReadAsync<PagedResult<AdminServerSummaryResponse>>(await SendAsync(HttpMethod.Get, $"{Base}/servers?filter[externalId]=service-42&filter[nodeId]={_nodeId}", token));
        Assert.Equal(created.Id, Assert.Single(filtered.Items).Id);

        var wrongNode = await ReadAsync<PagedResult<AdminServerSummaryResponse>>(await SendAsync(HttpMethod.Get, $"{Base}/servers?filter[externalId]=service-42&filter[nodeId]={_otherNodeId}", token));
        Assert.Empty(wrongNode.Items);

        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(HttpMethod.Post, $"{Base}/servers", token, ServerRequest("service-42"))).StatusCode);

        // Details: partial update, external id can be moved once it's free
        var patched = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Patch, $"{Base}/servers/{created.Id}/details", token,
            new ApplicationUpdateServerDetailsRequest { ExternalId = "service-43", Description = "Customer server" }));
        Assert.Equal("service-43", patched.ExternalId);
        Assert.Equal("Survival", patched.Name);
        Assert.Equal("Customer server", patched.Description);

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"{Base}/servers/external/service-42", token)).StatusCode);
    }

    [Fact]
    public async Task Servers_BuildStartupSuspendAndActivityAttribution()
    {
        var token = await FullAccessTokenAsync();
        var created = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Post, $"{Base}/servers", token, ServerRequest()));

        var build = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Patch, $"{Base}/servers/{created.Id}/build", token,
            new ApplicationUpdateServerBuildRequest { Limits = new ApplicationServerLimitsUpdate { Memory = 4096 } }));
        Assert.Equal(4096, build.Limits.Memory);
        Assert.Equal(created.Limits.Disk, build.Limits.Disk);
        Assert.Equal(created.PrimaryAllocation, build.PrimaryAllocation);

        var startup = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Patch, $"{Base}/servers/{created.Id}/startup", token,
            new ApplicationUpdateServerStartupRequest { Variables = new Dictionary<string, string> { ["SERVER_JARFILE"] = "paper.jar" } }));
        Assert.Equal("paper.jar", startup.Variables.Single(x => x.EnvVariable == "SERVER_JARFILE").Value);
        Assert.Equal("ghcr.io/java:21", startup.DockerImage);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, $"{Base}/servers/{created.Id}/suspend", token)).StatusCode);
        Assert.True((await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Get, $"{Base}/servers/{created.Id}", token))).Suspended);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, $"{Base}/servers/{created.Id}/unsuspend", token)).StatusCode);

        var activity = await WithScopeAsync(p => p.GetRequiredService<DataContext>().ServerActivities.AsNoTracking()
            .Where(x => x.ServerId == created.Id).ToListAsync());
        var entry = Assert.Single(activity, x => x.Event == ActivityEvents.StartupUpdate);
        Assert.Equal(_adminId, entry.UserId);
        Assert.StartsWith("mda_", entry.GetProperties()["applicationKey"]);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"{Base}/servers/{created.Id}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"{Base}/servers/{created.Id}", token)).StatusCode);
    }

    // ─── Nodes, allocations, shards, database hosts ─────────────────────────

    [Fact]
    public async Task Nodes_AndAllocations()
    {
        var token = await FullAccessTokenAsync();

        var created = await SendAsync(HttpMethod.Post, $"{Base}/nodes", token, new Shared.Http.Nodes.NodeRequest { Name = "node-3", Fqdn = "node3.example.com" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var node = await ReadAsync<Shared.Http.Nodes.NodeResponse>(created);

        var patched = await ReadAsync<Shared.Http.Nodes.NodeResponse>(await SendAsync(HttpMethod.Patch, $"{Base}/nodes/{node.Id}", token, new ApplicationUpdateNodeRequest { SftpPort = 2222 }));
        Assert.Equal(2222, patched.SftpPort);
        Assert.Equal("node3.example.com", patched.Fqdn);

        var configuration = await ReadAsync<Shared.Http.Nodes.NodeSetupResponse>(await SendAsync(HttpMethod.Get, $"{Base}/nodes/{node.Id}/configuration", token));
        Assert.Contains("\"sftpPort\": 2222", configuration.Configuration);

        var allocations = await ReadAsync<Shared.Http.Nodes.CreateAllocationsResponse>(await SendAsync(HttpMethod.Post, $"{Base}/nodes/{node.Id}/allocations", token,
            new Shared.Http.Nodes.CreateAllocationsRequest { IpAddress = "10.0.0.3", Ports = "30000-30002" }));
        Assert.Equal(3, allocations.Created);

        var list = await ReadAsync<PagedResult<Shared.Http.Nodes.AllocationResponse>>(await SendAsync(HttpMethod.Get, $"{Base}/nodes/{node.Id}/allocations?free=true", token));
        Assert.Equal(3, list.TotalCount);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"{Base}/nodes/{node.Id}/allocations/{list.Items[0].Id}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"{Base}/nodes/{node.Id}/allocations/{list.Items[0].Id}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"{Base}/nodes/999999/allocations", token)).StatusCode);

        // Assigned allocations can't be deleted
        var server = await ReadAsync<AdminServerDetailResponse>(await SendAsync(HttpMethod.Post, $"{Base}/servers", token,
            ServerRequest(deploy: new ApplicationDeployOptions { NodeId = node.Id })));
        var assigned = server.Allocations.Single().Id;
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(HttpMethod.Delete, $"{Base}/nodes/{node.Id}/allocations/{assigned}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(HttpMethod.Delete, $"{Base}/nodes/{node.Id}", token)).StatusCode);
    }

    [Fact]
    public async Task Nodes_ConfigurationNeedsReadWrite()
    {
        var token = await CreateTokenAsync(ApplicationResource.Nodes, ApplicationAccess.Read);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, $"{Base}/nodes/{_nodeId}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, $"{Base}/nodes/{_nodeId}/configuration", token)).StatusCode);
    }

    [Fact]
    public async Task Shards_AndDatabaseHosts()
    {
        var token = await FullAccessTokenAsync();

        var shards = await ReadAsync<PagedResult<Shared.Http.Shards.ShardSummaryResponse>>(await SendAsync(HttpMethod.Get, $"{Base}/shards", token));
        Assert.Equal(_shardId, Assert.Single(shards.Items).Id);

        var shard = await ReadAsync<Shared.Http.Shards.ShardDetailResponse>(await SendAsync(HttpMethod.Get, $"{Base}/shards/{_shardId}", token));
        Assert.Equal("Paper", shard.Name);

        var collections = await ReadAsync<PagedResult<Shared.Http.Shards.ShardCollectionResponse>>(await SendAsync(HttpMethod.Get, $"{Base}/shard-collections", token));
        Assert.Single(collections.Items);

        await WithScopeAsync(p => p.GetRequiredService<DatabaseHostService>().CreateAsync(new DatabaseHostRequest
        {
            Name = "mysql-1",
            Host = "db.example.com",
            Port = 3306,
            Username = "root",
            Password = "very-secret-password"
        }, CancellationToken.None));

        var hosts = await SendAsync(HttpMethod.Get, $"{Base}/database-hosts", token);
        var json = await hosts.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, hosts.StatusCode);
        Assert.Contains("mysql-1", json);
        Assert.DoesNotContain("very-secret-password", json);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Answers every demon request with 2xx and records the request lines</summary>
    private sealed class AcceptingDemon : HttpMessageHandler, IHttpClientFactory
    {
        public List<string> Requests { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests)
                Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");

            return Task.FromResult(new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.Accepted : HttpStatusCode.NoContent));
        }
    }
}
