using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Databases.Services;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.ApiServer.Features.Shards.Services;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.ApiServer.Features.Users.Services;
using Midnight.Shared;
using Midnight.Shared.Enums;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Demon;
using Midnight.Shared.Http.Nodes;
using Midnight.Shared.Http.Servers;
using Midnight.Shared.Http.Shards;

namespace Midnight.Panel.Tests;

public class ServerConfigurationBuilderTests
{
    private static Server CreateServer()
    {
        var shard = new Shard
        {
            Name = "Paper",
            StartupCommand = "java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}} --port {{SERVER_PORT}} --ip {{SERVER_IP}} --secret {{SECRET}}",
            StopCommand = "stop",
            StartupDone = ")! For help, type \r\nDone (\n\n",
            InstallScript = "#!/bin/bash\necho install",
            InstallImage = "installer:latest",
            InstallInterpreter = "/bin/sh",
            Variables =
            [
                new ShardVariable { Id = 1, EnvVariable = "SERVER_JARFILE", DefaultValue = "server.jar", SortOrder = 0 },
                new ShardVariable { Id = 2, EnvVariable = "SECRET", DefaultValue = "default-secret", UserViewable = false, SortOrder = 1 },
                new ShardVariable { Id = 3, EnvVariable = "VERSION", DefaultValue = "latest", SortOrder = 2 }
            ]
        };

        shard.SetConfigFiles([
            new ShardConfigFileDto
            {
                Path = "server.properties",
                Parser = "properties",
                Mappings = new Dictionary<string, string> { ["server-port"] = "{{SERVER_PORT}}", ["motd"] = "Jar {{SERVER_JARFILE}}" }
            }
        ]);
        shard.SetFeatures(new ShardFeaturesDto { Eula = true, PidLimit = 256 });

        var primary = new Allocation { Id = 10, IpAddress = "10.0.0.5", Port = 25565 };
        var additional = new Allocation { Id = 11, IpAddress = "10.0.0.5", Port = 25566 };

        return new Server
        {
            Uuid = Guid.Parse("6f2b5d43-35c3-4c43-bf35-4b8e0c4b0c11"),
            Name = "Survival",
            Node = new Node { Name = "Frankfurt 1" },
            Shard = shard,
            DockerImage = "ghcr.io/java:21",
            Memory = 2048,
            Cpu = 200,
            Disk = 10240,
            IoWeight = 500,
            PrimaryAllocationId = primary.Id,
            Allocations = [additional, primary],
            Variables =
            [
                new ServerVariable { ShardVariableId = 1, Value = "paper.jar" },
                new ServerVariable { ShardVariableId = 2, Value = "hidden" }
            ]
        };
    }

    [Fact]
    public void Build_ResolvesStartupCommandWithVariablesAndPorts()
    {
        var configuration = ServerConfigurationBuilder.Build(CreateServer(), "Europe/Berlin");

        Assert.Equal("java -Xmx2048M -jar paper.jar --port 25565 --ip 10.0.0.5 --secret hidden", configuration.StartupCommand);
        Assert.Equal(configuration.StartupCommand, configuration.Environment["STARTUP"]);
    }

    [Fact]
    public void Build_SetsEnvironment()
    {
        var server = CreateServer();
        var environment = ServerConfigurationBuilder.Build(server, null).Environment;

        Assert.Equal("paper.jar", environment["SERVER_JARFILE"]);
        Assert.Equal("hidden", environment["SECRET"]);
        Assert.Equal("latest", environment["VERSION"]); // no value stored: default
        Assert.Equal("2048", environment["SERVER_MEMORY"]);
        Assert.Equal("10.0.0.5", environment["SERVER_IP"]);
        Assert.Equal("25565", environment["SERVER_PORT"]);
        Assert.Equal(server.Uuid.ToString(), environment["P_SERVER_UUID"]);
        Assert.Equal("Frankfurt 1", environment["P_SERVER_LOCATION"]);
        Assert.Equal("UTC", environment["TZ"]);
    }

    [Fact]
    public void Build_UsesOwnStartupCommand()
    {
        var server = CreateServer();
        server.StartupCommand = "./start.sh {{VERSION}}";

        Assert.Equal("./start.sh latest", ServerConfigurationBuilder.Build(server, "UTC").StartupCommand);
    }

    [Fact]
    public void Build_PutsPrimaryAllocationFirst()
    {
        var configuration = ServerConfigurationBuilder.Build(CreateServer(), "UTC");

        Assert.Equal(25565, configuration.PrimaryAllocation.Port);
        Assert.Equal([25565, 25566], configuration.Allocations.Select(x => x.Port));
    }

    [Fact]
    public void Build_MapsProcessInstallAndLimits()
    {
        var configuration = ServerConfigurationBuilder.Build(CreateServer(), "UTC");

        Assert.Equal("stop", configuration.Process.StopCommand);
        Assert.Equal([")! For help, type", "Done ("], configuration.Process.StartupDone);
        Assert.True(configuration.Process.Features.Eula);
        Assert.Equal(256, configuration.Process.Features.PidLimit);

        var file = Assert.Single(configuration.Process.ConfigFiles);
        Assert.Equal("25565", file.Mappings["server-port"]);
        Assert.Equal("Jar paper.jar", file.Mappings["motd"]);

        Assert.Equal("installer:latest", configuration.Install.Image);
        Assert.Equal("/bin/sh", configuration.Install.Interpreter);
        Assert.Contains("echo install", configuration.Install.Script);

        Assert.Equal(2048, configuration.Limits.Memory);
        Assert.Equal(200, configuration.Limits.Cpu);
        Assert.Equal("ghcr.io/java:21", configuration.DockerImage);
    }

    [Fact]
    public void StartupPreview_KeepsHiddenVariables()
    {
        Assert.Equal("java -Xmx2048M -jar paper.jar --port 25565 --ip 10.0.0.5 --secret {{SECRET}}",
            ServerConfigurationBuilder.BuildStartupPreview(CreateServer()));
    }

    [Theory]
    [InlineData("^C", "SIGTERM", null, "SIGINT")]
    [InlineData("", "SIGKILL", null, "SIGKILL")]
    [InlineData(null, "", null, "SIGTERM")]
    [InlineData("end", "SIGTERM", "end", "SIGTERM")]
    public void GetStop_HandlesCtrlC(string? command, string signal, string? expectedCommand, string expectedSignal)
    {
        Assert.Equal((expectedCommand, expectedSignal), ServerConfigurationBuilder.GetStop(command, signal));
    }

    [Fact]
    public void FormatAddress_BracketsIpv6()
    {
        Assert.Equal("1.2.3.4:80", ServerConfigurationBuilder.FormatAddress("1.2.3.4", 80));
        Assert.Equal("[::1]:80", ServerConfigurationBuilder.FormatAddress("::1", 80));
    }
}

public class ServerAccessTests
{
    private static readonly Server Server = new() { OwnerId = 1 };

    internal static ClaimsPrincipal CreatePrincipal(int userId, string[] permissions, string[]? scopes = null)
    {
        var claims = new List<Claim>
        {
            new(MidnightClaims.UserId, userId.ToString()),
            new(MidnightClaims.AuthType, scopes == null ? MidnightClaims.AuthTypeSession : MidnightClaims.AuthTypeApiKey)
        };

        claims.AddRange(permissions.Select(x => new Claim(MidnightClaims.Permission, x)));
        claims.AddRange((scopes ?? []).Select(x => new Claim(MidnightClaims.Scope, x)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void Owner_HasEverything()
    {
        var owner = CreatePrincipal(1, Permissions.UserDefaults);

        Assert.Equal([ServerPermissions.All], ServerAccess.GetPermissions(owner, Server, null));
    }

    [Fact]
    public void OtherUser_HasNoAccess()
    {
        var other = CreatePrincipal(2, Permissions.UserDefaults);

        Assert.Null(ServerAccess.GetPermissions(other, Server, null));
    }

    [Fact]
    public void AdminWithReadOnly_GetsReadPermissions()
    {
        var admin = CreatePrincipal(2, [..Permissions.UserDefaults, Permissions.AdminServersRead]);
        var permissions = ServerAccess.GetPermissions(admin, Server, null)!;

        Assert.True(ServerPermissions.Has(permissions, ServerPermissions.FileReadContent));
        Assert.True(ServerPermissions.Has(permissions, ServerPermissions.WebsocketConnect));
        Assert.True(ServerPermissions.Has(permissions, ServerPermissions.BackupRead));
        Assert.False(ServerPermissions.Has(permissions, ServerPermissions.ControlStart));
        Assert.False(ServerPermissions.Has(permissions, ServerPermissions.FileUpdate));
        Assert.False(ServerPermissions.Has(permissions, ServerPermissions.DatabaseViewPassword));
    }

    [Fact]
    public void AdminWithWrite_HasEverything()
    {
        var admin = CreatePrincipal(2, [..Permissions.UserDefaults, "admin.servers.*"]);

        Assert.Equal([ServerPermissions.All], ServerAccess.GetPermissions(admin, Server, null));
    }

    [Fact]
    public void ApiKeyWithoutAdminScope_CantOpenForeignServers()
    {
        var apiKey = CreatePrincipal(2, ["*"], [Permissions.AccountRead]);

        Assert.Null(ServerAccess.GetPermissions(apiKey, Server, null));

        var scoped = CreatePrincipal(2, ["*"], [Permissions.AccountRead, Permissions.AdminServersRead]);
        var permissions = ServerAccess.GetPermissions(scoped, Server, null)!;

        Assert.True(ServerPermissions.Has(permissions, ServerPermissions.FileRead));
        Assert.False(ServerPermissions.Has(permissions, ServerPermissions.ControlStart));
    }

    [Fact]
    public void ReadOnlyApiKeyOfOwner_IsReducedToReadPermissions()
    {
        var apiKey = CreatePrincipal(1, ["*"], [Permissions.AccountRead]);
        var permissions = ServerAccess.GetPermissions(apiKey, Server, null)!;

        Assert.DoesNotContain(ServerPermissions.All, permissions);
        Assert.True(ServerPermissions.Has(permissions, ServerPermissions.FileRead));
        Assert.False(ServerPermissions.Has(permissions, ServerPermissions.ControlConsole));
    }
}

public class ConsoleTicketTests
{
    private readonly ConsoleTicketService _tickets = new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void Ticket_IsSingleUse()
    {
        var uuid = Guid.NewGuid();
        var ticket = _tickets.Create(5, uuid, [ServerPermissions.All]);

        var redeemed = _tickets.Redeem(ticket, uuid);

        Assert.NotNull(redeemed);
        Assert.Equal(5, redeemed.UserId);
        Assert.Equal([ServerPermissions.All], redeemed.Permissions);
        Assert.Null(_tickets.Redeem(ticket, uuid));
    }

    [Fact]
    public void Ticket_IsBoundToServer()
    {
        var ticket = _tickets.Create(5, Guid.NewGuid(), []);

        Assert.Null(_tickets.Redeem(ticket, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void UnknownTickets_AreRejected(string? ticket)
    {
        Assert.Null(_tickets.Redeem(ticket, Guid.NewGuid()));
    }

    [Fact]
    public void ParallelRedeem_OnlyOneWins()
    {
        var uuid = Guid.NewGuid();
        var ticket = _tickets.Create(1, uuid, []);

        var results = Enumerable.Range(0, 16).AsParallel().Select(_ => _tickets.Redeem(ticket, uuid)).ToList();

        Assert.Single(results, x => x != null);
    }
}

/// <summary>Runs the server service against an in-memory sqlite database and a fake demon</summary>
public sealed class ServerServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FakeDemon _demon = new();
    private ServiceProvider _services = null!;

    private int _ownerId;
    private int _otherUserId;
    private int _nodeId;
    private int _otherNodeId;
    private int _shardId;
    private List<int> _allocationIds = [];
    private int _otherNodeAllocationId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMemoryCache();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<AppConfiguration>().Configure(options => options.TimeZone = "Europe/Berlin");

        services.AddDbContext<SqliteDataContext>(options => options.UseSqlite(_connection));
        services.AddScoped<DataContext>(provider => provider.GetRequiredService<SqliteDataContext>());

        services.AddSingleton<IHttpClientFactory>(_demon);
        services.AddSingleton(new PasswordHasher(1024, 1, 1));
        services.AddSingleton<UserAuthCache>();
        services.AddScoped<NodeService>();
        services.AddScoped<NodeClient>();
        services.AddScoped<AllocationService>();
        services.AddScoped<ServerService>();
        services.AddSingleton<IDatabaseExecutor, FakeDatabaseExecutor>();
        services.AddScoped<DatabaseHostService>();
        services.AddScoped<ServerDatabaseService>();
        services.AddScoped<ShardService>();
        services.AddScoped<UserService>();

        _services = services.BuildServiceProvider();

        await RunAsync(async provider =>
        {
            var context = provider.GetRequiredService<DataContext>();
            await context.Database.EnsureCreatedAsync();

            var now = DateTimeOffset.UtcNow;
            var owner = new User { Username = "owner", Email = "owner@example.com", SecurityStamp = "a", CreatedAt = now };
            var other = new User { Username = "other", Email = "other@example.com", SecurityStamp = "b", CreatedAt = now };
            context.Users.AddRange(owner, other);
            await context.SaveChangesAsync();

            var nodes = provider.GetRequiredService<NodeService>();
            _nodeId = (await nodes.CreateAsync(new NodeRequest { Name = "node-1", Fqdn = "node1.example.com" }, CancellationToken.None)).Id;
            _otherNodeId = (await nodes.CreateAsync(new NodeRequest { Name = "node-2", Fqdn = "node2.example.com" }, CancellationToken.None)).Id;

            var allocations = Enumerable.Range(25565, 4).Select(port => new Allocation { NodeId = _nodeId, IpAddress = "10.0.0.1", Port = port }).ToList();
            var foreign = new Allocation { NodeId = _otherNodeId, IpAddress = "10.0.0.2", Port = 25565 };
            context.Allocations.AddRange(allocations);
            context.Allocations.Add(foreign);

            var collection = new ShardCollection { Name = "Minecraft", CreatedAt = now, UpdatedAt = now };
            var shard = new Shard
            {
                Collection = collection,
                Name = "Paper",
                DefaultDockerImage = "ghcr.io/java:21",
                StartupCommand = "java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}}",
                CreatedAt = now,
                UpdatedAt = now,
                Variables =
                [
                    new ShardVariable { Name = "Jar", EnvVariable = "SERVER_JARFILE", DefaultValue = "server.jar", Rules = "required|string|max:32" },
                    new ShardVariable { Name = "Version", EnvVariable = "VERSION", DefaultValue = "latest", Rules = "required|string", SortOrder = 1 },
                    new ShardVariable { Name = "Secret", EnvVariable = "SECRET", DefaultValue = "x", UserEditable = false, Rules = "string", SortOrder = 2 }
                ]
            };
            shard.SetDockerImages(new Dictionary<string, string> { ["Java 21"] = "ghcr.io/java:21", ["Java 17"] = "ghcr.io/java:17" });
            context.Shards.Add(shard);

            await context.SaveChangesAsync();

            _ownerId = owner.Id;
            _otherUserId = other.Id;
            _shardId = shard.Id;
            _allocationIds = allocations.Select(x => x.Id).ToList();
            _otherNodeAllocationId = foreign.Id;
            return 0;
        });
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private Task<T> WithServers<T>(Func<ServerService, Task<T>> action)
        => RunAsync(p => action(p.GetRequiredService<ServerService>()));

    private CreateServerRequest CreateRequest(int? primary = null, List<int>? additional = null) => new()
    {
        Name = "Survival",
        OwnerId = _ownerId,
        NodeId = _nodeId,
        ShardId = _shardId,
        PrimaryAllocationId = primary ?? _allocationIds[0],
        AdditionalAllocationIds = additional ?? [_allocationIds[1]],
        Limits = new ServerLimitsDto { Memory = 2048 },
        Variables = new Dictionary<string, string> { ["SERVER_JARFILE"] = "paper.jar" }
    };

    private Task<Server> CreateAsync(CreateServerRequest? request = null)
        => WithServers(s => s.CreateAsync(request ?? CreateRequest(), CancellationToken.None));

    private static async Task<int> StatusOf(Func<Task> action)
        => (await Assert.ThrowsAsync<HttpApiException>(action)).StatusCode;

    [Fact]
    public async Task Create_AssignsAllocationsAndSendsConfiguration()
    {
        var created = await CreateAsync();

        var server = await WithServers(s => s.GetAsync(created.Id, CancellationToken.None));

        Assert.Equal(ServerStatus.Installing, server.Status);
        Assert.Equal(_allocationIds[0], server.PrimaryAllocationId);
        Assert.Equal([_allocationIds[0], _allocationIds[1]], server.Allocations.Select(x => x.Id).Order());
        Assert.Equal("ghcr.io/java:21", server.DockerImage);

        var request = Assert.Single(_demon.Requests);
        Assert.Equal("POST /api/servers", request.Line);
        Assert.Equal("Bearer", request.Authorization?.Scheme);

        var body = JsonSerializer.Deserialize<DemonCreateServerRequest>(request.Body!, JsonDefaults.Options)!;
        Assert.Equal(server.Uuid, body.Configuration.Uuid);
        Assert.Equal("java -Xmx2048M -jar paper.jar", body.Configuration.StartupCommand);
        Assert.Equal("Europe/Berlin", body.Configuration.Environment["TZ"]);
        Assert.Equal("latest", body.Configuration.Environment["VERSION"]);
        Assert.Equal(2, body.Configuration.Allocations.Count);
    }

    [Fact]
    public async Task Create_SkipInstallIsReady()
    {
        var request = CreateRequest();
        request.SkipInstall = true;

        var server = await CreateAsync(request);

        Assert.Equal(ServerStatus.Ready, server.Status);
        Assert.NotNull(server.InstalledAt);
    }

    [Fact]
    public async Task Create_RejectsAllocationOfAnotherNode()
    {
        Assert.Equal(400, await StatusOf(() => CreateAsync(CreateRequest(additional: [_otherNodeAllocationId]))));
        Assert.Equal(400, await StatusOf(() => CreateAsync(CreateRequest(primary: 999_999))));
    }

    [Fact]
    public async Task Create_RejectsAllocationInUse()
    {
        await CreateAsync();

        Assert.Equal(409, await StatusOf(() => CreateAsync(CreateRequest(primary: _allocationIds[2], additional: [_allocationIds[1]]))));
    }

    [Fact]
    public async Task Create_RejectsForeignImage()
    {
        var request = CreateRequest();
        request.DockerImage = "evil/image:latest";

        Assert.Equal(400, await StatusOf(() => CreateAsync(request)));
    }

    [Fact]
    public async Task Create_ValidatesVariables()
    {
        var request = CreateRequest();
        request.Variables["SERVER_JARFILE"] = new string('a', 40);

        var exception = await Assert.ThrowsAsync<HttpApiException>(() => CreateAsync(request));

        Assert.Equal(400, exception.StatusCode);
        Assert.NotNull(exception.Errors);
        Assert.Contains("SERVER_JARFILE", exception.Errors.Keys);
    }

    [Fact]
    public async Task Create_NodeUnreachable_SavesNothing()
    {
        _demon.Fail = true;

        Assert.Equal(502, await StatusOf(() => CreateAsync()));

        var (servers, assigned) = await RunAsync(async p =>
        {
            var context = p.GetRequiredService<DataContext>();
            return (await context.Servers.CountAsync(), await context.Allocations.CountAsync(x => x.ServerId != null));
        });

        Assert.Equal(0, servers);
        Assert.Equal(0, assigned);
    }

    [Fact]
    public async Task UpdateBuild_ReplacesAllocations()
    {
        var created = await CreateAsync();

        await WithServers(s => s.UpdateBuildAsync(created.Id, new UpdateServerBuildRequest
        {
            Limits = new ServerLimitsDto { Memory = 4096 },
            PrimaryAllocationId = _allocationIds[1],
            AdditionalAllocationIds = [_allocationIds[2]]
        }, CancellationToken.None));

        var server = await WithServers(s => s.GetAsync(created.Id, CancellationToken.None));

        Assert.Equal(4096, server.Memory);
        Assert.Equal(_allocationIds[1], server.PrimaryAllocationId);
        Assert.Equal([_allocationIds[1], _allocationIds[2]], server.Allocations.Select(x => x.Id).Order());
        Assert.Contains(_demon.Requests, x => x.Line == $"PUT /api/servers/{server.Uuid}");

        // The old primary allocation is free again
        var free = await RunAsync(p => p.GetRequiredService<AllocationService>().ListAsync(_nodeId, new PagedQuery(), true, CancellationToken.None));
        Assert.Contains(free.Items, x => x.Id == _allocationIds[0]);
        Assert.DoesNotContain(free.Items, x => x.Id == _allocationIds[1]);
    }

    [Fact]
    public async Task UpdateUserVariables_OnlyEditableVariables()
    {
        var created = await CreateAsync();

        Task Update(string key, string value) => WithServers(async s =>
        {
            var server = await s.GetAsync(created.Id, CancellationToken.None);
            await s.UpdateUserVariablesAsync(server, new UpdateServerVariablesRequest { Variables = { [key] = value } }, CancellationToken.None);
            return 0;
        });

        Assert.Equal(400, await StatusOf(() => Update("SECRET", "changed")));
        Assert.Equal(400, await StatusOf(() => Update("UNKNOWN", "x")));
        Assert.Equal(400, await StatusOf(() => Update("VERSION", "")));

        await Update("VERSION", "1.21");

        var server = await WithServers(s => s.GetAsync(created.Id, CancellationToken.None));
        Assert.Equal("1.21", ServerConfigurationBuilder.GetVariableValues(server, viewableOnly: false)["VERSION"]);
    }

    [Fact]
    public async Task Suspended_RejectsPowerAndCommands()
    {
        var request = CreateRequest();
        request.SkipInstall = true;
        var created = await CreateAsync(request);

        await WithServers(async s => { await s.SetSuspendedAsync(created.Id, true, CancellationToken.None); return 0; });

        Assert.Equal(409, await StatusOf(() => WithServers(async s =>
        {
            await s.PowerAsync(await s.GetAsync(created.Id, CancellationToken.None), PowerAction.Start, CancellationToken.None);
            return 0;
        })));

        Assert.Equal(409, await StatusOf(() => WithServers(async s =>
        {
            await s.SendCommandAsync(await s.GetAsync(created.Id, CancellationToken.None), "say hi", CancellationToken.None);
            return 0;
        })));
    }

    [Fact]
    public async Task Delete_FreesAllocations()
    {
        var created = await CreateAsync();

        await WithServers(async s => { await s.DeleteAsync(created.Id, false, CancellationToken.None); return 0; });

        Assert.Contains(_demon.Requests, x => x.Line == $"DELETE /api/servers/{created.Uuid}");
        Assert.Equal(0, await RunAsync(p => p.GetRequiredService<DataContext>().Allocations.CountAsync(x => x.ServerId != null)));
    }

    [Fact]
    public async Task Delete_NodeUnreachable_NeedsForce()
    {
        var created = await CreateAsync();
        _demon.Fail = true;

        Assert.Equal(502, await StatusOf(() => WithServers(async s => { await s.DeleteAsync(created.Id, false, CancellationToken.None); return 0; })));

        await WithServers(async s => { await s.DeleteAsync(created.Id, true, CancellationToken.None); return 0; });

        Assert.Equal(0, await RunAsync(p => p.GetRequiredService<DataContext>().Servers.CountAsync()));
    }

    [Fact]
    public async Task OwnersShardsAndNodesWithServers_CantBeDeleted()
    {
        await CreateAsync();

        Assert.Equal(409, await StatusOf(() => RunAsync(async p =>
        {
            await p.GetRequiredService<UserService>().DeleteAsync(_ownerId, _otherUserId, CancellationToken.None);
            return 0;
        })));

        Assert.Equal(409, await StatusOf(() => RunAsync(async p =>
        {
            await p.GetRequiredService<ShardService>().DeleteAsync(_shardId, CancellationToken.None);
            return 0;
        })));

        Assert.Equal(409, await StatusOf(() => RunAsync(async p =>
        {
            await p.GetRequiredService<NodeService>().DeleteAsync(_nodeId, CancellationToken.None);
            return 0;
        })));
    }

    [Fact]
    public async Task InstallResult_OnlyForServersOfTheNode()
    {
        var created = await CreateAsync();

        Assert.Equal(404, await StatusOf(() => WithServers(async s =>
        {
            await s.SetInstallResultAsync(_otherNodeId, created.Uuid, true, CancellationToken.None);
            return 0;
        })));

        await WithServers(async s => { await s.SetInstallResultAsync(_nodeId, created.Uuid, true, CancellationToken.None); return 0; });

        var server = await WithServers(s => s.GetAsync(created.Id, CancellationToken.None));
        Assert.Equal(ServerStatus.Ready, server.Status);
        Assert.NotNull(server.InstalledAt);
    }

    [Fact]
    public async Task NodeConfigurations_OnlyContainServersOfTheNode()
    {
        var created = await CreateAsync();

        var own = await WithServers(s => s.ListNodeConfigurationsAsync(_nodeId, CancellationToken.None));
        var other = await WithServers(s => s.ListNodeConfigurationsAsync(_otherNodeId, CancellationToken.None));

        Assert.Equal(created.Uuid, Assert.Single(own).Uuid);
        Assert.Empty(other);
    }

    [Fact]
    public async Task ListForUser_OnlyOwnServers()
    {
        await CreateAsync();

        Assert.Single(await WithServers(s => s.ListForUserAsync(_ownerId, false, CancellationToken.None)));
        Assert.Empty(await WithServers(s => s.ListForUserAsync(_otherUserId, false, CancellationToken.None)));
        Assert.Single(await WithServers(s => s.ListForUserAsync(_otherUserId, true, CancellationToken.None)));
    }

    [Fact]
    public async Task Resources_NodeOffline()
    {
        var created = await CreateAsync();
        _demon.Fail = true;

        var resources = await WithServers(async s => await s.GetResourcesAsync(await s.GetAsync(created.Id, CancellationToken.None), CancellationToken.None));

        Assert.False(resources.NodeOnline);
        Assert.Equal(ServerState.Offline, resources.State);
    }

    /// <summary>Records the requests to the demon and answers them with 2xx (or connection errors when Fail is set)</summary>
    private sealed class FakeDemon : HttpMessageHandler, IHttpClientFactory
    {
        public List<RecordedRequest> Requests { get; } = [];
        public bool Fail { get; set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail)
                throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused");

            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            lock (Requests)
                Requests.Add(new RecordedRequest($"{request.Method} {request.RequestUri!.AbsolutePath}", request.Headers.Authorization, body));

            return new HttpResponseMessage(request.Method == HttpMethod.Post ? HttpStatusCode.Accepted : HttpStatusCode.NoContent);
        }
    }

    private sealed record RecordedRequest(string Line, System.Net.Http.Headers.AuthenticationHeaderValue? Authorization, string? Body);
}
