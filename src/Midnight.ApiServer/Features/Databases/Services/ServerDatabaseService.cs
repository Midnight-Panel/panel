using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Databases.Entities;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Databases.Services;

/// <summary>
/// Databases of a server on a database host: name s{serverId}_{suffix}, user u{serverId}_{random 10}, random 24 character password
/// (stored with data protection).
/// </summary>
public partial class ServerDatabaseService
{
    public const int PasswordLength = 24;
    public const int UsernameRandomLength = 10;

    private readonly DataContext _context;
    private readonly DatabaseHostService _hostService;
    private readonly IDatabaseExecutor _executor;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServerDatabaseService> _logger;

    public ServerDatabaseService(DataContext context, DatabaseHostService hostService, IDatabaseExecutor executor, IDataProtectionProvider dataProtection,
        TimeProvider timeProvider, ILogger<ServerDatabaseService> logger)
    {
        _context = context;
        _hostService = hostService;
        _executor = executor;
        _protector = dataProtection.CreateProtector("Midnight.ServerDatabase.v1");
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ─── Naming ─────────────────────────────────────────────────────────────

    [GeneratedRegex("^[a-zA-Z0-9_]{1,48}$")]
    private static partial Regex SuffixRegex();

    [GeneratedRegex(@"^[0-9a-zA-Z.%:_\-/]{1,64}$")]
    private static partial Regex RemoteRegex();

    /// <summary>s{serverId}_{suffix}, the suffix is validated again (the identifier ends up in SQL)</summary>
    public static string BuildDatabaseName(int serverId, string suffix)
    {
        var trimmed = suffix.Trim();

        if (!SuffixRegex().IsMatch(trimmed))
            throw InvalidField("Database", "Only letters, numbers and underscores (max. 48 characters) are allowed");

        return $"s{serverId}_{trimmed}";
    }

    public static string ValidateRemote(string remote)
    {
        var trimmed = remote.Trim();

        if (!RemoteRegex().IsMatch(trimmed))
            throw InvalidField("Remote", "Invalid connection source, use % for anywhere or an ip / host pattern");

        return trimmed;
    }

    /// <summary>u{serverId}_{10 random [a-z0-9]}, at most 32 characters like MySQL allows</summary>
    public static string GenerateUsername(int serverId)
        => $"u{serverId}_{SecureTokens.GenerateAlphanumeric(UsernameRandomLength)}";

    public static string GeneratePassword()
        => RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", PasswordLength);

    // ─── Queries ────────────────────────────────────────────────────────────

    public async Task<ServerDatabaseListResponse> ListAsync(Server server, bool includePasswords, CancellationToken cancellationToken)
    {
        var databases = await _context.ServerDatabases
            .AsNoTracking()
            .Include(x => x.DatabaseHost)
            .Where(x => x.ServerId == server.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return new ServerDatabaseListResponse
        {
            Databases = databases.Select(x => ToResponse(x, includePasswords)).ToList(),
            Limit = server.DatabaseLimit
        };
    }

    public async Task<ServerDatabase> GetAsync(Server server, int id, CancellationToken cancellationToken)
        => await _context.ServerDatabases.Include(x => x.DatabaseHost).FirstOrDefaultAsync(x => x.Id == id && x.ServerId == server.Id, cancellationToken)
           ?? throw HttpApiException.NotFound("Database not found");

    // ─── Actions ────────────────────────────────────────────────────────────

    public async Task<ServerDatabase> CreateAsync(Server server, CreateServerDatabaseRequest request, CancellationToken cancellationToken)
    {
        var name = BuildDatabaseName(server.Id, request.Database);
        var remote = ValidateRemote(request.Remote);

        if (server.DatabaseLimit <= 0)
            throw HttpApiException.Conflict("Databases are disabled", "This server can't have databases");

        var count = await _context.ServerDatabases.CountAsync(x => x.ServerId == server.Id, cancellationToken);

        if (count >= server.DatabaseLimit)
            throw HttpApiException.Conflict("Database limit reached", $"This server can have at most {server.DatabaseLimit} database(s)");

        if (await _context.ServerDatabases.AnyAsync(x => x.ServerId == server.Id && x.Name == name, cancellationToken))
            throw HttpApiException.Conflict("A database with this name already exists");

        var host = await SelectHostAsync(server.NodeId, cancellationToken)
                   ?? throw HttpApiException.Conflict("No database host available", "There is no database host with free capacity for this server's node");

        var username = GenerateUsername(server.Id);
        var password = GeneratePassword();

        try
        {
            await _executor.CreateDatabaseAsync(_hostService.GetConnection(host), name, username, remote, password, cancellationToken);
        }
        catch (DatabaseHostException e)
        {
            _logger.LogWarning("Could not create database {Database} for server {ServerUuid} on host {HostId}: {Error}", name, server.Uuid, host.Id, e.Message);
            throw new HttpApiException(StatusCodes.Status502BadGateway, "Could not create the database", "The database host is not reachable or rejected the request");
        }

        var database = new ServerDatabase
        {
            ServerId = server.Id,
            DatabaseHost = host,
            DatabaseHostId = host.Id,
            Name = name,
            Username = username,
            Password = _protector.Protect(password),
            Remote = remote,
            CreatedAt = _timeProvider.GetUtcNow()
        };

        _context.ServerDatabases.Add(database);
        await _context.SaveChangesAsync(cancellationToken);

        return database;
    }

    public async Task<ServerDatabase> RotatePasswordAsync(Server server, int id, CancellationToken cancellationToken)
    {
        var database = await GetAsync(server, id, cancellationToken);
        var password = GeneratePassword();

        try
        {
            await _executor.ChangePasswordAsync(_hostService.GetConnection(database.DatabaseHost), database.Username, database.Remote, password, cancellationToken);
        }
        catch (DatabaseHostException e)
        {
            _logger.LogWarning("Could not rotate the password of database {DatabaseId}: {Error}", database.Id, e.Message);
            throw new HttpApiException(StatusCodes.Status502BadGateway, "Could not change the password", "The database host is not reachable or rejected the request");
        }

        database.Password = _protector.Protect(password);
        await _context.SaveChangesAsync(cancellationToken);

        return database;
    }

    /// <summary>Drops user and database on the host, the entry stays when the host fails (502)</summary>
    public async Task<ServerDatabase> DeleteAsync(Server server, int id, CancellationToken cancellationToken)
    {
        var database = await GetAsync(server, id, cancellationToken);

        try
        {
            await _executor.DropDatabaseAsync(_hostService.GetConnection(database.DatabaseHost), database.Name, database.Username, database.Remote, cancellationToken);
        }
        catch (DatabaseHostException e)
        {
            _logger.LogWarning("Could not drop database {DatabaseId}: {Error}", database.Id, e.Message);
            throw new HttpApiException(StatusCodes.Status502BadGateway, "Could not delete the database", "The database host is not reachable or rejected the request");
        }

        _context.ServerDatabases.Remove(database);
        await _context.SaveChangesAsync(cancellationToken);

        return database;
    }

    /// <summary>Used when a server is deleted: drops every database, failures are only logged (the entries are removed by cascade)</summary>
    public async Task DropAllAsync(int serverId, CancellationToken cancellationToken)
    {
        var databases = await _context.ServerDatabases.AsNoTracking().Include(x => x.DatabaseHost).Where(x => x.ServerId == serverId).ToListAsync(cancellationToken);

        foreach (var database in databases)
        {
            try
            {
                await _executor.DropDatabaseAsync(_hostService.GetConnection(database.DatabaseHost), database.Name, database.Username, database.Remote, cancellationToken);
            }
            catch (Exception e) when (e is DatabaseHostException or CryptographicException)
            {
                _logger.LogWarning("Could not drop database {Database} on host {HostId} while deleting server {ServerId}: {Error}",
                    database.Name, database.DatabaseHostId, serverId, e.Message);
            }
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Hosts of the server's node first, then hosts for all nodes; full hosts (MaxDatabases) are skipped</summary>
    public async Task<DatabaseHost?> SelectHostAsync(int nodeId, CancellationToken cancellationToken)
    {
        var candidates = await _context.DatabaseHosts
            .Where(x => x.NodeId == nodeId || x.NodeId == null)
            .Select(x => new { Host = x, Count = x.Databases.Count })
            .ToListAsync(cancellationToken);

        return candidates
            .Where(x => x.Host.MaxDatabases == null || x.Count < x.Host.MaxDatabases)
            .OrderBy(x => x.Host.NodeId == null ? 1 : 0)
            .ThenBy(x => x.Count)
            .ThenBy(x => x.Host.Id)
            .Select(x => x.Host)
            .FirstOrDefault();
    }

    public ServerDatabaseResponse ToResponse(ServerDatabase database, bool includePassword) => new()
    {
        Id = database.Id,
        Name = database.Name,
        Username = database.Username,
        Password = includePassword ? _protector.Unprotect(database.Password) : null,
        Remote = database.Remote,
        Host = database.DatabaseHost.Host,
        Port = database.DatabaseHost.Port,
        CreatedAt = database.CreatedAt
    };

    private static HttpApiException InvalidField(string field, string message)
        => new(StatusCodes.Status400BadRequest, "Invalid " + field.ToLowerInvariant(), message) { Errors = new Dictionary<string, string> { [field] = message } };
}
