using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Databases.Entities;
using Midnight.ApiServer.Features.Databases.Services;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.ApiServer.Features.Servers.Http;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.Shared;
using Midnight.Shared.Enums;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Admin;
using Midnight.Shared.Http.Demon;
using Midnight.Shared.Http.Nodes;
using Midnight.Shared.Http.Servers;

namespace Midnight.Panel.Tests;

// ─── Pure logic ─────────────────────────────────────────────────────────────

public class ServerPermissionModelTests
{
    private static readonly Server Server = new() { Id = 7, OwnerId = 1 };

    private static ClaimsPrincipal User(int id, params string[] extra)
        => ServerAccessTests.CreatePrincipal(id, [..Permissions.UserDefaults, ..extra]);

    [Fact]
    public void Matrix()
    {
        // owner, admin write: everything
        Assert.Equal([ServerPermissions.All], ServerAccess.GetPermissions(User(1), Server, null));
        Assert.Equal([ServerPermissions.All], ServerAccess.GetPermissions(User(2, Permissions.AdminServersWrite), Server, null));

        // subuser: exactly the list (unknown entries dropped)
        var subuser = ServerAccess.GetPermissions(User(3), Server, [ServerPermissions.FileRead, "file.*", "bogus"])!;
        Assert.Equal([ServerPermissions.FileRead], subuser);

        // subuser without permissions still has access (detail page) but nothing else
        Assert.Empty(ServerAccess.GetPermissions(User(3), Server, [])!);

        // admin read + subuser: union
        var both = ServerAccess.GetPermissions(User(3, Permissions.AdminServersRead), Server, [ServerPermissions.ControlStart])!;
        Assert.Contains(ServerPermissions.ControlStart, both);
        Assert.Contains(ServerPermissions.FileReadContent, both);

        // stranger
        Assert.Null(ServerAccess.GetPermissions(User(4), Server, null));
    }

    [Fact]
    public void ReadPermissions_AreOnlyReads()
    {
        Assert.All(ServerAccess.ReadPermissions, x => Assert.True(
            x.EndsWith(".read") || x is ServerPermissions.FileReadContent or ServerPermissions.WebsocketConnect));
        Assert.Contains(ServerPermissions.ActivityRead, ServerAccess.ReadPermissions);
    }

    [Fact]
    public void EnsurePermission_Is403()
    {
        Assert.Equal(403, Assert.Throws<HttpApiException>(() => ServerAccess.EnsurePermission([ServerPermissions.FileRead], ServerPermissions.FileDelete)).StatusCode);
        ServerAccess.EnsurePermission([ServerPermissions.All], ServerPermissions.FileDelete);
    }

    [Theory]
    [InlineData(PowerAction.Start, ServerPermissions.ControlStart)]
    [InlineData(PowerAction.Stop, ServerPermissions.ControlStop)]
    [InlineData(PowerAction.Kill, ServerPermissions.ControlStop)]
    [InlineData(PowerAction.Restart, ServerPermissions.ControlRestart)]
    public void PowerPermissions(PowerAction action, string permission)
        => Assert.Equal(permission, ServerAccess.GetPowerPermission(action));

    [Fact]
    public void Grant_OnlyOwnPermissions()
    {
        string[] actor = [ServerPermissions.FileRead, ServerPermissions.UserCreate];

        Assert.Equal([ServerPermissions.FileRead], ServerAccess.ValidateGrant(actor, [ServerPermissions.FileRead, ServerPermissions.FileRead]));
        Assert.Equal(403, Assert.Throws<HttpApiException>(() => ServerAccess.ValidateGrant(actor, [ServerPermissions.FileDelete])).StatusCode);
        Assert.Equal(400, Assert.Throws<HttpApiException>(() => ServerAccess.ValidateGrant([ServerPermissions.All], ["*"])).StatusCode);
        Assert.Equal(400, Assert.Throws<HttpApiException>(() => ServerAccess.ValidateGrant([ServerPermissions.All], ["file.nope"])).StatusCode);

        Assert.Equal(ServerPermissions.AllPermissions.Distinct().Count(),
            ServerAccess.ValidateGrant([ServerPermissions.All], ServerPermissions.AllPermissions.ToList()).Count);
    }

    [Theory]
    [InlineData("""{"event":"command","data":"say hi"}""", ServerPermissions.ControlConsole)]
    [InlineData("""{"event":"power","data":"start"}""", ServerPermissions.ControlStart)]
    [InlineData("""{"event":"power","data":"Kill"}""", ServerPermissions.ControlStop)]
    [InlineData("""{"event":"power","data":"restart"}""", ServerPermissions.ControlRestart)]
    public void ConsoleMessages_NeedTheirPermission(string json, string permission)
    {
        var (error, required, _) = ConsoleProxy.ParseBrowserMessage(Encoding.UTF8.GetBytes(json));

        Assert.Null(error);
        Assert.Equal(permission, required);
    }

    [Theory]
    [InlineData("""{"event":"power","data":"explode"}""")]
    [InlineData("""{"event":"power","data":"1"}""")]
    [InlineData("""{"event":"command","data":""}""")]
    [InlineData("""{"event":"stats"}""")]
    [InlineData("not json")]
    public void ConsoleMessages_InvalidAreRejected(string json)
        => Assert.NotNull(ConsoleProxy.ParseBrowserMessage(Encoding.UTF8.GetBytes(json)).Error);

    [Fact]
    public void Activity_TruncatesValues()
    {
        var properties = ActivityService.Sanitize(new Dictionary<string, string?> { ["command"] = new string('x', 500), ["empty"] = null });

        Assert.Equal(ActivityService.MaxValueLength, properties["command"].Length);
        Assert.False(properties.ContainsKey("empty"));
    }

    [Fact]
    public void BackupDownloadName_IsSafe()
    {
        Assert.Equal("Backup at 2026-01-01 10_00.tar.gz", ServerBackupsController.GetDownloadName("Backup at 2026-01-01 10:00"));
        Assert.Equal("_.._x.tar.gz", ServerBackupsController.GetDownloadName("/../x"));
        Assert.Equal("backup.tar.gz", ServerBackupsController.GetDownloadName("  "));
    }

    [Fact]
    public void FilesPath_EscapesQuery()
    {
        var uuid = Guid.Parse("6f2b5d43-35c3-4c43-bf35-4b8e0c4b0c11");

        Assert.Equal($"api/servers/{uuid}/files/list?directory=%2Fa%20b%26c%3D1", NodeClient.FilesPath(uuid, "list", "directory", "/a b&c=1"));
        Assert.Equal($"api/servers/{uuid}/files/compress", NodeClient.FilesPath(uuid, "compress"));
    }
}

public class DownloadTicketTests
{
    private readonly DownloadTicketService _tickets = new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void FileTicket_IsSingleUse()
    {
        var uuid = Guid.NewGuid();
        var ticket = _tickets.CreateForFile(3, uuid, "/logs/latest.log");

        var redeemed = _tickets.Redeem(ticket, uuid, DownloadKind.File);

        Assert.NotNull(redeemed);
        Assert.Equal("/logs/latest.log", redeemed.Target);
        Assert.Equal(3, redeemed.UserId);
        Assert.Null(_tickets.Redeem(ticket, uuid, DownloadKind.File));
    }

    [Fact]
    public void Ticket_IsBoundToServerAndKind()
    {
        var uuid = Guid.NewGuid();

        Assert.Null(_tickets.Redeem(_tickets.CreateForFile(1, uuid, "/a"), Guid.NewGuid(), DownloadKind.File));
        Assert.Null(_tickets.Redeem(_tickets.CreateForFile(1, uuid, "/a"), uuid, DownloadKind.Backup));

        var backup = Guid.NewGuid();
        Assert.Equal(backup.ToString(), _tickets.Redeem(_tickets.CreateForBackup(1, uuid, backup), uuid, DownloadKind.Backup)?.Target);
    }

    [Fact]
    public void ParallelRedeem_OnlyOneWins()
    {
        var uuid = Guid.NewGuid();
        var ticket = _tickets.CreateForFile(1, uuid, "/a");

        var results = Enumerable.Range(0, 16).AsParallel().Select(_ => _tickets.Redeem(ticket, uuid, DownloadKind.File)).ToList();

        Assert.Single(results, x => x != null);
    }
}

public class ScheduleCronTests
{
    [Fact]
    public void InvalidParts_ReportTheirField()
    {
        var exception = Assert.Throws<HttpApiException>(() => ScheduleService.ParseCron("*/5", "25", "*", "13", "*"));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(["Hour", "Month"], exception.Errors!.Keys.Order());
    }

    [Theory]
    [InlineData("0 0", "*")]
    [InlineData("", "*")]
    [InlineData("*", "a")]
    public void Garbage_IsRejected(string minute, string hour)
        => Assert.Equal(400, Assert.Throws<HttpApiException>(() => ScheduleService.ParseCron(minute, hour, "*", "*", "*")).StatusCode);

    [Fact]
    public void NextRun_UsesPanelTimeZone()
    {
        var expression = ScheduleService.ParseCron("0", "3", "*", "*", "1-5");
        var berlin = PanelTimeZone.Get("Europe/Berlin");

        // Friday 2026-10-30 12:00 UTC → next weekday 03:00 Berlin = Monday 2026-11-02 03:00 CET = 02:00 UTC
        var next = ScheduleService.GetNextRun(expression, new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero), berlin);

        Assert.Equal(new DateTimeOffset(2026, 11, 2, 2, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void UnknownTimeZone_FallsBackToUtc()
        => Assert.Equal(TimeZoneInfo.Utc, PanelTimeZone.Get("Mars/Olympus"));

    [Theory]
    [InlineData(ScheduleTaskAction.Power, "restart", "Restart")]
    [InlineData(ScheduleTaskAction.Command, "  say hi ", "say hi")]
    [InlineData(ScheduleTaskAction.Backup, "logs/\r\n\r\n*.tmp ", "logs/\n*.tmp")]
    public void Payload_IsNormalized(ScheduleTaskAction action, string payload, string expected)
        => Assert.Equal(expected, ScheduleService.NormalizePayload(action, payload));

    [Theory]
    [InlineData(ScheduleTaskAction.Power, "explode")]
    [InlineData(ScheduleTaskAction.Power, "2")]
    [InlineData(ScheduleTaskAction.Command, " ")]
    public void InvalidPayload_IsRejected(ScheduleTaskAction action, string payload)
        => Assert.Equal(400, Assert.Throws<HttpApiException>(() => ScheduleService.NormalizePayload(action, payload)).StatusCode);
}

public class DatabaseNamingTests
{
    [Fact]
    public void DatabaseName_IsPrefixed()
    {
        Assert.Equal("s12_minecraft", ServerDatabaseService.BuildDatabaseName(12, "minecraft"));
        Assert.Equal("s12_" + new string('a', 48), ServerDatabaseService.BuildDatabaseName(12, new string('a', 48)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a-b")]
    [InlineData("a`; DROP DATABASE x; --")]
    [InlineData("name with space")]
    [InlineData("äöü")]
    public void InvalidSuffix_IsRejected(string suffix)
        => Assert.Equal(400, Assert.Throws<HttpApiException>(() => ServerDatabaseService.BuildDatabaseName(1, suffix)).StatusCode);

    [Fact]
    public void Username_And_Password()
    {
        var username = ServerDatabaseService.GenerateUsername(123456);

        Assert.Matches("^u123456_[a-z0-9]{10}$", username);
        Assert.True(username.Length <= 32);
        Assert.NotEqual(username, ServerDatabaseService.GenerateUsername(123456));

        var password = ServerDatabaseService.GeneratePassword();
        Assert.Matches("^[a-zA-Z0-9]{24}$", password);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("10.0.0.%")]
    [InlineData("192.168.1.0/255.255.255.0")]
    public void Remote_Valid(string remote) => Assert.Equal(remote, ServerDatabaseService.ValidateRemote(remote));

    [Theory]
    [InlineData("a b")]
    [InlineData("x'y")]
    [InlineData("`")]
    public void Remote_Invalid(string remote)
        => Assert.Equal(400, Assert.Throws<HttpApiException>(() => ServerDatabaseService.ValidateRemote(remote)).StatusCode);

    [Fact]
    public void Identifiers_AreQuoted()
    {
        Assert.Equal("`s1_db`", MySqlDatabaseExecutor.QuoteIdentifier("s1_db"));
        Assert.Equal("`a``b`", MySqlDatabaseExecutor.QuoteIdentifier("a`b"));
        Assert.Equal("`u1_x`@`%`", MySqlDatabaseExecutor.Account("u1_x", "%"));
        Assert.Equal(@"`s1\_my\_db`", MySqlDatabaseExecutor.QuoteGrantDatabase("s1_my_db"));
    }
}

// ─── Services against sqlite and a fake demon ───────────────────────────────

public sealed class ServerFeatureServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FeatureDemon _demon = new();
    private readonly FakeDatabaseExecutor _executor = new();
    private ServiceProvider _services = null!;

    private int _ownerId;
    private int _subuserId;
    private int _strangerId;
    private int _nodeId;
    private int _otherNodeId;
    private int _shardId;
    private List<Allocation> _allocations = [];

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<AppConfiguration>().Configure(options => options.TimeZone = "Europe/Berlin");

        services.AddDbContext<SqliteDataContext>(options => options.UseSqlite(_connection));
        services.AddScoped<DataContext>(provider => provider.GetRequiredService<SqliteDataContext>());

        services.AddSingleton<IHttpClientFactory>(_demon);
        services.AddSingleton<IDatabaseExecutor>(_executor);
        services.AddScoped<NodeService>();
        services.AddScoped<NodeClient>();
        services.AddScoped<ServerService>();
        services.AddScoped<ServerAccessService>();
        services.AddScoped<ActivityService>();
        services.AddScoped<SubuserService>();
        services.AddScoped<BackupService>();
        services.AddScoped<ServerNetworkService>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<ScheduleRunner>();
        services.AddSingleton<ScheduleDispatcher>();
        services.AddSingleton<ScheduleWorker>();
        services.AddScoped<DatabaseHostService>();
        services.AddScoped<ServerDatabaseService>();

        _services = services.BuildServiceProvider();

        await RunAsync(async provider =>
        {
            var context = provider.GetRequiredService<DataContext>();
            await context.Database.EnsureCreatedAsync();

            var now = DateTimeOffset.UtcNow;
            var owner = new User { Username = "owner", Email = "owner@example.com", SecurityStamp = "a", CreatedAt = now };
            var subuser = new User { Username = "helper", Email = "helper@example.com", SecurityStamp = "b", CreatedAt = now };
            var stranger = new User { Username = "stranger", Email = "stranger@example.com", SecurityStamp = "c", CreatedAt = now };
            context.Users.AddRange(owner, subuser, stranger);
            await context.SaveChangesAsync();

            var nodes = provider.GetRequiredService<NodeService>();
            _nodeId = (await nodes.CreateAsync(new NodeRequest { Name = "node-1", Fqdn = "node1.example.com" }, CancellationToken.None)).Id;
            _otherNodeId = (await nodes.CreateAsync(new NodeRequest { Name = "node-2", Fqdn = "node2.example.com" }, CancellationToken.None)).Id;

            // Primary ip 10.0.0.1, one more port there and two on another ip
            _allocations =
            [
                new Allocation { NodeId = _nodeId, IpAddress = "10.0.0.1", Port = 25565 },
                new Allocation { NodeId = _nodeId, IpAddress = "10.0.0.2", Port = 25570 },
                new Allocation { NodeId = _nodeId, IpAddress = "10.0.0.2", Port = 25571 },
                new Allocation { NodeId = _nodeId, IpAddress = "10.0.0.1", Port = 25566 },
                new Allocation { NodeId = _otherNodeId, IpAddress = "10.0.0.9", Port = 25565 }
            ];
            context.Allocations.AddRange(_allocations);

            var shard = new Shard
            {
                Collection = new ShardCollection { Name = "Minecraft", CreatedAt = now, UpdatedAt = now },
                Name = "Paper",
                DefaultDockerImage = "ghcr.io/java:21",
                StartupCommand = "java -jar server.jar",
                CreatedAt = now,
                UpdatedAt = now,
                Variables = [new ShardVariable { Name = "Version", EnvVariable = "VERSION", DefaultValue = "latest", Rules = "required|string" }]
            };
            context.Shards.Add(shard);
            await context.SaveChangesAsync();

            _ownerId = owner.Id;
            _subuserId = subuser.Id;
            _strangerId = stranger.Id;
            _shardId = shard.Id;
            return 0;
        });
    }

    public async Task DisposeAsync()
    {
        await _services.GetRequiredService<ScheduleDispatcher>().WhenIdleAsync();
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private Task RunAsync(Func<IServiceProvider, Task> action) => RunAsync(async p => { await action(p); return 0; });

    private static async Task<int> StatusOf(Func<Task> action)
        => (await Assert.ThrowsAsync<HttpApiException>(action)).StatusCode;

    private static ClaimsPrincipal Principal(int userId, params string[] extra)
        => ServerAccessTests.CreatePrincipal(userId, [..Permissions.UserDefaults, ..extra]);

    private async Task<Server> CreateServerAsync(int backups = 2, int allocations = 1, int databases = 2)
    {
        var created = await RunAsync(p => p.GetRequiredService<ServerService>().CreateAsync(new CreateServerRequest
        {
            Name = "Survival",
            OwnerId = _ownerId,
            NodeId = _nodeId,
            ShardId = _shardId,
            PrimaryAllocationId = _allocations[0].Id,
            Limits = new ServerLimitsDto(),
            FeatureLimits = new ServerFeatureLimitsDto { Backups = backups, Allocations = allocations, Databases = databases },
            SkipInstall = true
        }, CancellationToken.None));

        _demon.Requests.Clear();
        return created;
    }

    private Task<Server> LoadAsync(IServiceProvider provider, Server server)
        => provider.GetRequiredService<ServerService>().GetAsync(server.Id, CancellationToken.None);

    private Task AddSubuserAsync(Server server, params string[] permissions) => RunAsync(async p =>
    {
        var subuser = new Subuser { ServerId = server.Id, UserId = _subuserId };
        subuser.SetPermissions(permissions);
        var context = p.GetRequiredService<DataContext>();
        context.Subusers.Add(subuser);
        await context.SaveChangesAsync();
    });

    // ─── Access ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Access_Matrix()
    {
        var server = await CreateServerAsync();
        await AddSubuserAsync(server, ServerPermissions.FileRead);

        Task<ServerAccessContext> Get(ClaimsPrincipal user, string? permission)
            => RunAsync(p => p.GetRequiredService<ServerAccessService>().GetAsync(user, server.Uuid, permission, CancellationToken.None));

        Assert.Equal([ServerPermissions.All], (await Get(Principal(_ownerId), ServerPermissions.FileDelete)).Permissions);
        Assert.Equal([ServerPermissions.FileRead], (await Get(Principal(_subuserId), ServerPermissions.FileRead)).Permissions);
        Assert.Equal(403, await StatusOf(() => Get(Principal(_subuserId), ServerPermissions.FileDelete)));
        Assert.Equal(404, await StatusOf(() => Get(Principal(_strangerId), null)));
        Assert.Equal(403, await StatusOf(() => Get(Principal(_strangerId, Permissions.AdminServersRead), ServerPermissions.ControlStart)));
        await Get(Principal(_strangerId, Permissions.AdminServersRead), ServerPermissions.FileReadContent);
        await Get(Principal(_strangerId, Permissions.AdminServersWrite), ServerPermissions.ControlStart);
    }

    [Fact]
    public async Task Detail_HidesVariablesWithoutStartupRead()
    {
        var server = await CreateServerAsync();
        var loaded = await RunAsync(p => LoadAsync(p, server));

        Assert.Empty(ServerService.ToDetail(loaded, _subuserId, [ServerPermissions.FileRead]).Variables);

        var detail = ServerService.ToDetail(loaded, _subuserId, [ServerPermissions.StartupRead]);
        Assert.Single(detail.Variables);
        Assert.Equal([ServerPermissions.StartupRead], detail.Permissions);
        Assert.Equal(server.Id, detail.Id);
        Assert.False(detail.IsOwner);
    }

    [Fact]
    public async Task List_IncludesSubuserServers()
    {
        var server = await CreateServerAsync();

        Assert.Empty(await RunAsync(p => p.GetRequiredService<ServerService>().ListForUserAsync(_subuserId, false, CancellationToken.None)));

        await AddSubuserAsync(server);

        var listed = await RunAsync(p => p.GetRequiredService<ServerService>().ListForUserAsync(_subuserId, false, CancellationToken.None));
        Assert.False(ServerService.ToSummary(Assert.Single(listed), _subuserId).IsOwner);
        Assert.Empty(await RunAsync(p => p.GetRequiredService<ServerService>().ListForUserAsync(_strangerId, false, CancellationToken.None)));
    }

    // ─── Subusers ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Subusers_CreateRules()
    {
        var server = await CreateServerAsync();

        Task Create(ClaimsPrincipal actor, string email, params string[] permissions) => RunAsync(async p =>
        {
            var access = await p.GetRequiredService<ServerAccessService>().GetAsync(actor, server.Uuid, ServerPermissions.UserCreate, CancellationToken.None);
            await p.GetRequiredService<SubuserService>().CreateAsync(access, new CreateSubuserRequest { Email = email, Permissions = [..permissions] }, CancellationToken.None);
        });

        var owner = Principal(_ownerId);

        Assert.Equal(400, await StatusOf(() => Create(owner, "nobody@example.com")));
        Assert.Equal(400, await StatusOf(() => Create(owner, "OWNER@example.com")));

        await Create(owner, " Helper@Example.com ", ServerPermissions.UserCreate, ServerPermissions.FileRead);
        Assert.Equal(409, await StatusOf(() => Create(owner, "helper@example.com")));

        // The subuser can invite, but only with permissions they have
        var helper = Principal(_subuserId);
        Assert.Equal(403, await StatusOf(() => Create(helper, "stranger@example.com", ServerPermissions.FileDelete)));
        await Create(helper, "stranger@example.com", ServerPermissions.FileRead);

        var list = await RunAsync(p => p.GetRequiredService<SubuserService>().ListAsync(server, CancellationToken.None));
        Assert.Equal(["helper", "stranger"], list.Select(x => x.Username));
        Assert.Equal([ServerPermissions.FileRead, ServerPermissions.UserCreate], list[0].Permissions);
    }

    [Fact]
    public async Task Subusers_CantChangeThemselves_AndAreRemovedWithTheUser()
    {
        var server = await CreateServerAsync();
        await AddSubuserAsync(server, ServerPermissions.UserUpdate, ServerPermissions.UserDelete);

        var id = await RunAsync(p => p.GetRequiredService<DataContext>().Subusers.Select(x => x.Id).SingleAsync());

        Assert.Equal(403, await StatusOf(() => RunAsync(async p =>
        {
            var access = await p.GetRequiredService<ServerAccessService>().GetAsync(Principal(_subuserId), server.Uuid, null, CancellationToken.None);
            await p.GetRequiredService<SubuserService>().UpdateAsync(access, id, new UpdateSubuserRequest { Permissions = [ServerPermissions.UserUpdate] }, CancellationToken.None);
        })));

        await RunAsync(async p =>
        {
            var context = p.GetRequiredService<DataContext>();
            context.Users.Remove(await context.Users.SingleAsync(x => x.Id == _subuserId));
            await context.SaveChangesAsync();
        });

        Assert.Equal(0, await RunAsync(p => p.GetRequiredService<DataContext>().Subusers.CountAsync()));
    }

    // ─── Backups ────────────────────────────────────────────────────────────

    private Task<Backup> CreateBackupAsync(Server server, bool locked = false)
        => RunAsync(async p => await p.GetRequiredService<BackupService>().CreateAsync(await LoadAsync(p, server), null, null, locked, CancellationToken.None));

    private Task<Backup> ReportAsync(Backup backup, bool successful, int? nodeId = null)
        => RunAsync(p => p.GetRequiredService<BackupService>().SetResultAsync(nodeId ?? _nodeId, backup.Uuid,
            new DemonBackupResultRequest { Successful = successful, Bytes = 1024, Checksum = "sha256:abc" }, CancellationToken.None));

    [Fact]
    public async Task Backups_DisabledWithLimitZero()
    {
        var server = await CreateServerAsync(backups: 0);

        Assert.Equal(409, await StatusOf(() => CreateBackupAsync(server)));
        Assert.Empty(_demon.Requests);
    }

    [Fact]
    public async Task Backups_LimitAndRunning()
    {
        var server = await CreateServerAsync(backups: 2);

        var first = await CreateBackupAsync(server);
        Assert.StartsWith("Backup at ", first.Name);
        Assert.Contains(_demon.Requests, x => x.Line == $"POST /api/servers/{server.Uuid}/backups" && x.Body!.Contains(first.Uuid.ToString()));

        // One running backup at a time
        Assert.Equal(409, await StatusOf(() => CreateBackupAsync(server)));

        await ReportAsync(first, successful: true);
        var second = await CreateBackupAsync(server);
        await ReportAsync(second, successful: false);

        // Failed backups don't count
        var third = await CreateBackupAsync(server);
        await ReportAsync(third, successful: true);

        var exception = await Assert.ThrowsAsync<HttpApiException>(() => CreateBackupAsync(server));
        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("Backup limit reached", exception.Title);

        // Nothing was deleted automatically
        Assert.Equal(3, await RunAsync(p => p.GetRequiredService<DataContext>().Backups.CountAsync()));
    }

    [Fact]
    public async Task Backups_NodeFailure_SavesNothing()
    {
        var server = await CreateServerAsync();
        _demon.Fail = true;

        Assert.Equal(502, await StatusOf(() => CreateBackupAsync(server)));
        Assert.Equal(0, await RunAsync(p => p.GetRequiredService<DataContext>().Backups.CountAsync()));
    }

    [Fact]
    public async Task Backups_LockAndDeleteRules()
    {
        var server = await CreateServerAsync();
        var backup = await CreateBackupAsync(server, locked: true);

        Task Delete() => RunAsync(async p => await p.GetRequiredService<BackupService>().DeleteAsync(await LoadAsync(p, server), backup.Uuid, CancellationToken.None));
        Task<Backup> Toggle() => RunAsync(async p => await p.GetRequiredService<BackupService>().ToggleLockAsync(await LoadAsync(p, server), backup.Uuid, CancellationToken.None));

        Assert.Equal(409, await StatusOf(Delete)); // locked
        Assert.False((await Toggle()).IsLocked);
        Assert.Equal(409, await StatusOf(Delete)); // still running

        // Restore and download need a successful backup
        Assert.Equal(409, await StatusOf(() => RunAsync(async p =>
            await p.GetRequiredService<BackupService>().RestoreAsync(await LoadAsync(p, server), backup.Uuid, false, CancellationToken.None))));

        await ReportAsync(backup, successful: true);

        await RunAsync(async p => await p.GetRequiredService<BackupService>().RestoreAsync(await LoadAsync(p, server), backup.Uuid, true, CancellationToken.None));
        Assert.Contains(_demon.Requests, x => x.Line == $"POST /api/servers/{server.Uuid}/backups/{backup.Uuid}/restore" && x.Body!.Contains("true"));

        await Delete();
        Assert.Contains(_demon.Requests, x => x.Line == $"DELETE /api/servers/{server.Uuid}/backups/{backup.Uuid}");
        Assert.Equal(0, await RunAsync(p => p.GetRequiredService<DataContext>().Backups.CountAsync()));
    }

    [Fact]
    public async Task Backups_DemonNotFoundCountsAsDeleted()
    {
        var server = await CreateServerAsync();
        var backup = await CreateBackupAsync(server);
        await ReportAsync(backup, successful: false);

        _demon.NotFound = true;
        await RunAsync(async p => await p.GetRequiredService<BackupService>().DeleteAsync(await LoadAsync(p, server), backup.Uuid, CancellationToken.None));

        Assert.Equal(0, await RunAsync(p => p.GetRequiredService<DataContext>().Backups.CountAsync()));
    }

    [Fact]
    public async Task RemoteBackupResult_OnlyForOwnNode()
    {
        var server = await CreateServerAsync();
        var backup = await CreateBackupAsync(server);

        Assert.Equal(404, await StatusOf(() => ReportAsync(backup, successful: true, nodeId: _otherNodeId)));
        Assert.Equal(404, await StatusOf(() => RunAsync(p => p.GetRequiredService<BackupService>().GetForNodeAsync(_otherNodeId, backup.Uuid, CancellationToken.None))));

        var reported = await ReportAsync(backup, successful: true);

        Assert.True(reported.IsSuccessful);
        Assert.Equal(1024, reported.Bytes);
        Assert.Equal("sha256:abc", reported.Checksum);
        Assert.NotNull(reported.CompletedAt);
    }

    // ─── Network ────────────────────────────────────────────────────────────

    private Task<Allocation> AssignAsync(Server server)
        => RunAsync(async p => await p.GetRequiredService<ServerNetworkService>().AssignAsync(await LoadAsync(p, server), CancellationToken.None));

    [Fact]
    public async Task Allocations_AutoAssignPrefersPrimaryIpAndRespectsLimit()
    {
        var server = await CreateServerAsync(allocations: 2);

        var first = await AssignAsync(server);
        Assert.Equal(_allocations[3].Id, first.Id); // same ip as the primary
        Assert.Contains(_demon.Requests, x => x.Line == $"PUT /api/servers/{server.Uuid}");

        var second = await AssignAsync(server);
        Assert.Equal("10.0.0.2", second.IpAddress);

        var exception = await Assert.ThrowsAsync<HttpApiException>(() => AssignAsync(server));
        Assert.Equal("Allocation limit reached", exception.Title);

        var list = ServerNetworkService.ToList(await RunAsync(p => LoadAsync(p, server)));
        Assert.Equal(3, list.Allocations.Count);
        Assert.Equal(2, list.Limit);
    }

    [Fact]
    public async Task Allocations_LimitZeroAndNoneFree()
    {
        var disabled = await CreateServerAsync(allocations: 0);
        Assert.Equal(409, await StatusOf(() => AssignAsync(disabled)));

        await RunAsync(async p =>
        {
            var context = p.GetRequiredService<DataContext>();
            await context.Allocations.Where(x => x.ServerId == null).ExecuteUpdateAsync(x => x.SetProperty(a => a.NodeId, _otherNodeId));
        });

        await RunAsync(async p =>
        {
            var context = p.GetRequiredService<DataContext>();
            var server = await context.Servers.SingleAsync();
            server.AllocationLimit = 5;
            await context.SaveChangesAsync();
        });

        var exception = await Assert.ThrowsAsync<HttpApiException>(() => AssignAsync(disabled));
        Assert.Equal("No free allocation available", exception.Title);
    }

    [Fact]
    public async Task Allocations_NodeFailureRollsBack()
    {
        var server = await CreateServerAsync();
        _demon.Fail = true;

        Assert.Equal(502, await StatusOf(() => AssignAsync(server)));
        Assert.Equal(1, await RunAsync(p => p.GetRequiredService<DataContext>().Allocations.CountAsync(x => x.ServerId != null)));
    }

    [Fact]
    public async Task Allocations_PrimaryRules()
    {
        var server = await CreateServerAsync();
        var extra = await AssignAsync(server);

        Task Remove(int id) => RunAsync(async p => await p.GetRequiredService<ServerNetworkService>().RemoveAsync(await LoadAsync(p, server), id, CancellationToken.None));

        Assert.Equal(409, await StatusOf(() => Remove(_allocations[0].Id)));
        Assert.Equal(404, await StatusOf(() => Remove(_allocations[4].Id))); // not the server's

        await RunAsync(async p =>
        {
            var network = p.GetRequiredService<ServerNetworkService>();
            var loaded = await LoadAsync(p, server);
            await network.UpdateNotesAsync(loaded, extra.Id, " Query ", CancellationToken.None);
            await network.SetPrimaryAsync(loaded, extra.Id, CancellationToken.None);
        });

        await Remove(_allocations[0].Id);

        var reloaded = await RunAsync(p => LoadAsync(p, server));
        Assert.Equal(extra.Id, reloaded.PrimaryAllocationId);
        Assert.Equal("Query", Assert.Single(reloaded.Allocations).Notes);

        var freed = await RunAsync(p => p.GetRequiredService<DataContext>().Allocations.SingleAsync(x => x.Id == _allocations[0].Id));
        Assert.Null(freed.ServerId);
    }

    // ─── Schedules ──────────────────────────────────────────────────────────

    private Task<Schedule> CreateScheduleAsync(Server server, bool onlyWhenOnline = false, bool active = true)
        => RunAsync(async p => await p.GetRequiredService<ScheduleService>().CreateAsync(await LoadAsync(p, server), new ScheduleRequest
        {
            Name = "Nightly",
            Minute = "0",
            Hour = "4",
            IsActive = active,
            OnlyWhenOnline = onlyWhenOnline
        }, CancellationToken.None));

    private Task<ScheduleTask> AddTaskAsync(Server server, Schedule schedule, ScheduleTaskAction action, string payload, bool continueOnFailure = false)
        => RunAsync(async p => await p.GetRequiredService<ScheduleService>().AddTaskAsync(await LoadAsync(p, server), schedule.Id,
            new ScheduleTaskRequest { Action = action, Payload = payload, ContinueOnFailure = continueOnFailure }, CancellationToken.None));

    [Fact]
    public async Task Schedules_NextRunAndTasks()
    {
        var server = await CreateServerAsync();
        var schedule = await CreateScheduleAsync(server);

        Assert.NotNull(schedule.NextRunAt);
        Assert.True(schedule.NextRunAt > DateTimeOffset.UtcNow);
        Assert.Null((await CreateScheduleAsync(server, active: false)).NextRunAt);

        for (var i = 0; i < ScheduleService.MaxTasks; i++)
            await AddTaskAsync(server, schedule, ScheduleTaskAction.Command, $"say {i}");

        Assert.Equal(409, await StatusOf(() => AddTaskAsync(server, schedule, ScheduleTaskAction.Command, "say 10")));

        var tasks = await RunAsync(p => p.GetRequiredService<DataContext>().ScheduleTasks.OrderBy(x => x.Sequence).ToListAsync());
        Assert.Equal(Enumerable.Range(1, 10), tasks.Select(x => x.Sequence));

        await RunAsync(async p => await p.GetRequiredService<ScheduleService>().DeleteTaskAsync(await LoadAsync(p, server), schedule.Id, tasks[2].Id, CancellationToken.None));

        var remaining = await RunAsync(p => p.GetRequiredService<DataContext>().ScheduleTasks.OrderBy(x => x.Sequence).ToListAsync());
        Assert.Equal(Enumerable.Range(1, 9), remaining.Select(x => x.Sequence));
        Assert.Equal("say 3", remaining[2].Payload);
    }

    [Fact]
    public void ScheduleTasks_NeedTheUsersPermissions()
    {
        var access = new ServerAccessContext(new Server(), [ServerPermissions.ScheduleUpdate, ServerPermissions.ControlStart], 1);

        ServerSchedulesController.EnsureTaskAllowed(access, new ScheduleTaskRequest { Action = ScheduleTaskAction.Power, Payload = "start" });

        Assert.Equal(403, Assert.Throws<HttpApiException>(() => ServerSchedulesController.EnsureTaskAllowed(access,
            new ScheduleTaskRequest { Action = ScheduleTaskAction.Power, Payload = "kill" })).StatusCode);
        Assert.Equal(403, Assert.Throws<HttpApiException>(() => ServerSchedulesController.EnsureTaskAllowed(access,
            new ScheduleTaskRequest { Action = ScheduleTaskAction.Command, Payload = "op me" })).StatusCode);
        Assert.Equal(403, Assert.Throws<HttpApiException>(() => ServerSchedulesController.EnsureTaskAllowed(access,
            new ScheduleTaskRequest { Action = ScheduleTaskAction.Backup })).StatusCode);
    }

    [Fact]
    public async Task ScheduleRun_StopsOnFailureUnlessContinue()
    {
        var server = await CreateServerAsync(backups: 0);
        var schedule = await CreateScheduleAsync(server);

        await AddTaskAsync(server, schedule, ScheduleTaskAction.Backup, "", continueOnFailure: true); // fails: backups disabled
        await AddTaskAsync(server, schedule, ScheduleTaskAction.Command, "say continued");
        await AddTaskAsync(server, schedule, ScheduleTaskAction.Backup, ""); // fails and stops
        await AddTaskAsync(server, schedule, ScheduleTaskAction.Power, "Restart");

        var result = await RunScheduleAsync(schedule);

        Assert.Equal(ScheduleRunResult.Failed, result);
        Assert.Contains(_demon.Requests, x => x.Line == $"POST /api/servers/{server.Uuid}/commands" && x.Body!.Contains("say continued"));
        Assert.DoesNotContain(_demon.Requests, x => x.Line.EndsWith("/power"));

        var stored = await RunAsync(p => p.GetRequiredService<DataContext>().Schedules.SingleAsync(x => x.Id == schedule.Id));
        Assert.False(stored.IsProcessing);
        Assert.NotNull(stored.LastRunAt);

        var activity = await RunAsync(p => p.GetRequiredService<ActivityService>().ListAsync(server.Id, 1, 10, CancellationToken.None));
        var entry = Assert.Single(activity.Items, x => x.Event == ActivityEvents.ScheduleRun);
        Assert.Equal("failed", entry.Properties["result"]);
        Assert.Null(entry.UserId);
    }

    [Fact]
    public async Task ScheduleRun_OnlyWhenOnlineSkips()
    {
        var server = await CreateServerAsync();
        var schedule = await CreateScheduleAsync(server, onlyWhenOnline: true);
        await AddTaskAsync(server, schedule, ScheduleTaskAction.Command, "say hi");

        _demon.State = ServerState.Offline;
        Assert.Equal(ScheduleRunResult.Skipped, await RunScheduleAsync(schedule));
        Assert.DoesNotContain(_demon.Requests, x => x.Line.EndsWith("/commands"));

        _demon.State = ServerState.Online;
        Assert.Equal(ScheduleRunResult.Completed, await RunScheduleAsync(schedule));
        Assert.Contains(_demon.Requests, x => x.Line.EndsWith("/commands"));
    }

    [Fact]
    public async Task ScheduleDispatcher_ClaimsOnce_AndWorkerRunsDueSchedules()
    {
        var server = await CreateServerAsync();
        var schedule = await CreateScheduleAsync(server);
        await AddTaskAsync(server, schedule, ScheduleTaskAction.Command, "say due");

        // Due one minute ago, already "running" from before a restart
        await RunAsync(p => p.GetRequiredService<DataContext>().Schedules.ExecuteUpdateAsync(x => x
            .SetProperty(s => s.NextRunAt, DateTimeOffset.UtcNow.AddMinutes(-1))
            .SetProperty(s => s.IsProcessing, true)));

        var dispatcher = _services.GetRequiredService<ScheduleDispatcher>();
        Assert.False(await dispatcher.TryStartAsync(schedule.Id, true, ActivityActor.System, CancellationToken.None));

        var worker = _services.GetRequiredService<ScheduleWorker>();
        await worker.ResetAsync(CancellationToken.None);
        await worker.RunDueAsync(CancellationToken.None);
        await dispatcher.WhenIdleAsync();

        Assert.Contains(_demon.Requests, x => x.Line.EndsWith("/commands") && x.Body!.Contains("say due"));

        var stored = await RunAsync(p => p.GetRequiredService<DataContext>().Schedules.SingleAsync());
        Assert.False(stored.IsProcessing);
        Assert.True(stored.NextRunAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ScheduleWorker_SkipsLongMissedRuns()
    {
        var server = await CreateServerAsync();
        var schedule = await CreateScheduleAsync(server);

        await RunAsync(p => p.GetRequiredService<DataContext>().Schedules.ExecuteUpdateAsync(x => x.SetProperty(s => s.NextRunAt, DateTimeOffset.UtcNow.AddHours(-3))));

        await _services.GetRequiredService<ScheduleWorker>().ResetAsync(CancellationToken.None);

        var stored = await RunAsync(p => p.GetRequiredService<DataContext>().Schedules.SingleAsync(x => x.Id == schedule.Id));
        Assert.True(stored.NextRunAt > DateTimeOffset.UtcNow);
    }

    private async Task<ScheduleRunResult> RunScheduleAsync(Schedule schedule)
    {
        await RunAsync(p => p.GetRequiredService<DataContext>().Schedules.Where(x => x.Id == schedule.Id).ExecuteUpdateAsync(x => x.SetProperty(s => s.IsProcessing, true)));
        return await RunAsync(p => p.GetRequiredService<ScheduleRunner>().RunAsync(schedule.Id, false, ActivityActor.System, CancellationToken.None));
    }

    // ─── Databases ──────────────────────────────────────────────────────────

    private Task<DatabaseHostResponse> CreateHostAsync(string name, int? nodeId, int? max = null)
        => RunAsync(p => p.GetRequiredService<DatabaseHostService>().CreateAsync(new DatabaseHostRequest
        {
            Name = name,
            Host = name + ".db.local",
            Username = "root",
            Password = "secret-" + name,
            NodeId = nodeId,
            MaxDatabases = max
        }, CancellationToken.None));

    private Task<ServerDatabase> CreateDatabaseAsync(Server server, string suffix)
        => RunAsync(async p => await p.GetRequiredService<ServerDatabaseService>().CreateAsync(await LoadAsync(p, server),
            new CreateServerDatabaseRequest { Database = suffix, Remote = "%" }, CancellationToken.None));

    [Fact]
    public async Task Databases_NoHost_And_Disabled()
    {
        var disabled = await CreateServerAsync(databases: 0);

        Assert.Equal(409, await StatusOf(() => CreateDatabaseAsync(disabled, "main")));

        await RunAsync(async p =>
        {
            var context = p.GetRequiredService<DataContext>();
            (await context.Servers.SingleAsync()).DatabaseLimit = 1;
            await context.SaveChangesAsync();
        });

        var exception = await Assert.ThrowsAsync<HttpApiException>(() => CreateDatabaseAsync(disabled, "main"));
        Assert.Equal(409, exception.StatusCode);
        Assert.Contains("host", exception.Title);
    }

    [Fact]
    public async Task Databases_CreateRotateDelete()
    {
        var server = await CreateServerAsync(databases: 2);
        await CreateHostAsync("global", null);
        var nodeHost = await CreateHostAsync("local", _nodeId, max: 1);
        await CreateHostAsync("foreign", _otherNodeId);

        var first = await CreateDatabaseAsync(server, "main");

        Assert.Equal($"s{server.Id}_main", first.Name);
        Assert.Equal(nodeHost.Id, first.DatabaseHostId); // node host first
        Assert.Matches($"^u{server.Id}_[a-z0-9]{{10}}$", first.Username);

        var created = Assert.Single(_executor.Calls);
        Assert.Equal(("create", "local.db.local", "root", "secret-local", first.Name), (created.Action, created.Host.Host, created.Host.Username, created.Host.Password, created.Database));
        Assert.NotEqual(created.Password, first.Password); // stored encrypted

        Assert.Equal(409, await StatusOf(() => CreateDatabaseAsync(server, "main")));

        // The node host is full (MaxDatabases 1): the global host is used
        var second = await CreateDatabaseAsync(server, "logs");
        Assert.Equal("global.db.local", _executor.Calls.Last().Host.Host);

        var limit = await Assert.ThrowsAsync<HttpApiException>(() => CreateDatabaseAsync(server, "third"));
        Assert.Equal("Database limit reached", limit.Title);

        var list = await RunAsync(async p => await p.GetRequiredService<ServerDatabaseService>().ListAsync(await LoadAsync(p, server), false, CancellationToken.None));
        Assert.All(list.Databases, x => Assert.Null(x.Password));

        var withPasswords = await RunAsync(async p => await p.GetRequiredService<ServerDatabaseService>().ListAsync(await LoadAsync(p, server), true, CancellationToken.None));
        Assert.Equal(created.Password, withPasswords.Databases.Single(x => x.Id == first.Id).Password);

        await RunAsync(async p => await p.GetRequiredService<ServerDatabaseService>().RotatePasswordAsync(await LoadAsync(p, server), first.Id, CancellationToken.None));
        var rotated = _executor.Calls.Last();
        Assert.Equal("password", rotated.Action);
        Assert.NotEqual(created.Password, rotated.Password);

        await RunAsync(async p => await p.GetRequiredService<ServerDatabaseService>().DeleteAsync(await LoadAsync(p, server), second.Id, CancellationToken.None));
        Assert.Equal(("drop", second.Name), (_executor.Calls.Last().Action, _executor.Calls.Last().Database));

        // Hosts with databases can't be deleted
        Assert.Equal(409, await StatusOf(() => RunAsync(p => p.GetRequiredService<DatabaseHostService>().DeleteAsync(nodeHost.Id, CancellationToken.None))));
    }

    [Fact]
    public async Task Databases_HostFailure_SavesNothing()
    {
        var server = await CreateServerAsync();
        await CreateHostAsync("global", null);
        _executor.Fail = true;

        Assert.Equal(502, await StatusOf(() => CreateDatabaseAsync(server, "main")));
        Assert.Equal(0, await RunAsync(p => p.GetRequiredService<DataContext>().ServerDatabases.CountAsync()));

        var test = await RunAsync(async p =>
        {
            var hosts = p.GetRequiredService<DatabaseHostService>();
            return await hosts.TestAsync((await hosts.ListAsync(CancellationToken.None)).Single().Id, CancellationToken.None);
        });
        Assert.False(test.Success);
        Assert.NotNull(test.Error);
    }

    [Fact]
    public async Task DeletingServer_DropsDatabasesBestEffortAndRemovesEverything()
    {
        var server = await CreateServerAsync();
        await CreateHostAsync("global", null);
        await CreateDatabaseAsync(server, "main");
        await CreateBackupAsync(server);
        await CreateScheduleAsync(server);
        await AddSubuserAsync(server, ServerPermissions.FileRead);

        _executor.Fail = true; // logged, doesn't block the delete

        await RunAsync(p => p.GetRequiredService<ServerService>().DeleteAsync(server.Id, false, CancellationToken.None));

        Assert.Equal("drop", _executor.Calls.Last().Action);

        await RunAsync(async p =>
        {
            var context = p.GetRequiredService<DataContext>();
            Assert.Equal(0, await context.ServerDatabases.CountAsync());
            Assert.Equal(0, await context.Backups.CountAsync());
            Assert.Equal(0, await context.Schedules.CountAsync());
            Assert.Equal(0, await context.Subusers.CountAsync());
            Assert.Equal(0, await context.ServerActivities.CountAsync());
        });
    }

    [Fact]
    public async Task DatabaseHost_PasswordKeptOnUpdate()
    {
        var host = await CreateHostAsync("global", null);

        Assert.Equal(400, await StatusOf(() => RunAsync(p => p.GetRequiredService<DatabaseHostService>().CreateAsync(
            new DatabaseHostRequest { Name = "nopw", Host = "x", Username = "root" }, CancellationToken.None))));
        Assert.Equal(409, await StatusOf(() => CreateHostAsync("GLOBAL", null)));

        await RunAsync(p => p.GetRequiredService<DatabaseHostService>().UpdateAsync(host.Id,
            new DatabaseHostRequest { Name = "global", Host = "new.local", Username = "admin", Port = 3307 }, CancellationToken.None));

        var connection = await RunAsync(async p =>
        {
            var hosts = p.GetRequiredService<DatabaseHostService>();
            return hosts.GetConnection(await hosts.GetAsync(host.Id, CancellationToken.None));
        });

        Assert.Equal(new DatabaseHostConnection("new.local", 3307, "admin", "secret-global"), connection);

        var stored = await RunAsync(p => p.GetRequiredService<DataContext>().DatabaseHosts.SingleAsync());
        Assert.DoesNotContain("secret", stored.Password);
    }

    // ─── Fake demon ─────────────────────────────────────────────────────────

    /// <summary>Records requests, answers GET /api/servers/{uuid} with <see cref="State"/> and everything else with 2xx</summary>
    private sealed class FeatureDemon : HttpMessageHandler, IHttpClientFactory
    {
        public List<RecordedRequest> Requests { get; } = [];
        public bool Fail { get; set; }
        public bool NotFound { get; set; }
        public ServerState State { get; set; } = ServerState.Online;

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail)
                throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused");

            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var path = request.RequestUri!.AbsolutePath;

            lock (Requests)
                Requests.Add(new RecordedRequest($"{request.Method} {path}", body));

            if (NotFound)
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            if (request.Method == HttpMethod.Get && path.Count(x => x == '/') == 3 && path.StartsWith("/api/servers/"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new DemonServerStateResponse { Uuid = Guid.Parse(path[13..]), State = State }, options: JsonDefaults.Options)
                };
            }

            return new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.Accepted : HttpStatusCode.NoContent);
        }
    }

    private sealed record RecordedRequest(string Line, string? Body);
}

/// <summary>Records the statements instead of talking to MySQL</summary>
internal sealed class FakeDatabaseExecutor : IDatabaseExecutor
{
    public List<(string Action, DatabaseHostConnection Host, string? Database, string Username, string? Password)> Calls { get; } = [];
    public bool Fail { get; set; }

    public Task CreateDatabaseAsync(DatabaseHostConnection host, string database, string username, string remote, string password, CancellationToken cancellationToken)
        => Record("create", host, database, username, password);

    public Task ChangePasswordAsync(DatabaseHostConnection host, string username, string remote, string password, CancellationToken cancellationToken)
        => Record("password", host, null, username, password);

    public Task DropDatabaseAsync(DatabaseHostConnection host, string database, string username, string remote, CancellationToken cancellationToken)
        => Record("drop", host, database, username, null);

    public Task<string> TestAsync(DatabaseHostConnection host, CancellationToken cancellationToken)
        => Fail ? throw new DatabaseHostException("Connection refused") : Task.FromResult("8.4.0");

    private Task Record(string action, DatabaseHostConnection host, string? database, string username, string? password)
    {
        lock (Calls)
            Calls.Add((action, host, database, username, password));

        return Fail ? throw new DatabaseHostException("Connection refused") : Task.CompletedTask;
    }
}
