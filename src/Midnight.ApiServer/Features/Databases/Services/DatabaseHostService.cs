using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Databases.Entities;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.Databases.Services;

/// <summary>Admin management of the MySQL / MariaDB hosts. Passwords are stored with data protection like node tokens.</summary>
public class DatabaseHostService
{
    private readonly DataContext _context;
    private readonly IDatabaseExecutor _executor;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _timeProvider;

    public DatabaseHostService(DataContext context, IDatabaseExecutor executor, IDataProtectionProvider dataProtection, TimeProvider timeProvider)
    {
        _context = context;
        _executor = executor;
        _protector = dataProtection.CreateProtector("Midnight.DatabaseHost.v1");
        _timeProvider = timeProvider;
    }

    public async Task<List<DatabaseHostResponse>> ListAsync(CancellationToken cancellationToken)
        => await Project(_context.DatabaseHosts.AsNoTracking().OrderBy(x => x.Name)).ToListAsync(cancellationToken);

    public async Task<DatabaseHostResponse> GetResponseAsync(int id, CancellationToken cancellationToken)
        => await Project(_context.DatabaseHosts.AsNoTracking().Where(x => x.Id == id)).FirstOrDefaultAsync(cancellationToken)
           ?? throw HttpApiException.NotFound("Database host not found");

    public async Task<DatabaseHost> GetAsync(int id, CancellationToken cancellationToken)
        => await _context.DatabaseHosts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
           ?? throw HttpApiException.NotFound("Database host not found");

    public async Task<DatabaseHostResponse> CreateAsync(DatabaseHostRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Password))
            throw new HttpApiException(StatusCodes.Status400BadRequest, "Password required", "A new database host needs a password")
            {
                Errors = new Dictionary<string, string> { ["Password"] = "The password is required" }
            };

        var host = new DatabaseHost { CreatedAt = _timeProvider.GetUtcNow() };
        await ApplyAsync(host, request, null, cancellationToken);

        _context.DatabaseHosts.Add(host);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(host.Id, cancellationToken);
    }

    /// <summary>A null or empty password keeps the stored one</summary>
    public async Task<DatabaseHostResponse> UpdateAsync(int id, DatabaseHostRequest request, CancellationToken cancellationToken)
    {
        var host = await GetAsync(id, cancellationToken);
        await ApplyAsync(host, request, id, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetResponseAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var host = await GetAsync(id, cancellationToken);
        var databases = await _context.ServerDatabases.CountAsync(x => x.DatabaseHostId == id, cancellationToken);

        if (databases > 0)
            throw HttpApiException.Conflict("The database host still has databases", $"Delete the {databases} database(s) on this host first");

        _context.DatabaseHosts.Remove(host);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Connects with a short timeout, connection problems are reported in the response</summary>
    public async Task<DatabaseHostTestResponse> TestAsync(int id, CancellationToken cancellationToken)
    {
        var host = await GetAsync(id, cancellationToken);

        try
        {
            var version = await _executor.TestAsync(GetConnection(host), cancellationToken);
            return new DatabaseHostTestResponse { Success = true, Version = version };
        }
        catch (DatabaseHostException e)
        {
            return new DatabaseHostTestResponse { Success = false, Error = e.Message };
        }
    }

    public DatabaseHostConnection GetConnection(DatabaseHost host)
        => new(host.Host, host.Port, host.Username, _protector.Unprotect(host.Password));

    private async Task ApplyAsync(DatabaseHost host, DatabaseHostRequest request, int? exceptId, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var lower = name.ToLower();

        if (await _context.DatabaseHosts.AnyAsync(x => x.Id != exceptId && x.Name.ToLower() == lower, cancellationToken))
            throw HttpApiException.Conflict("A database host with this name already exists");

        if (request.NodeId != null && !await _context.Nodes.AnyAsync(x => x.Id == request.NodeId, cancellationToken))
            throw HttpApiException.BadRequest("Node not found", "The selected node does not exist");

        host.Name = name;
        host.Host = request.Host.Trim();
        host.Port = request.Port;
        host.Username = request.Username.Trim();
        host.NodeId = request.NodeId;
        host.MaxDatabases = request.MaxDatabases;

        if (!string.IsNullOrEmpty(request.Password))
            host.Password = _protector.Protect(request.Password);
    }

    private static IQueryable<DatabaseHostResponse> Project(IQueryable<DatabaseHost> hosts) => hosts.Select(x => new DatabaseHostResponse
    {
        Id = x.Id,
        Name = x.Name,
        Host = x.Host,
        Port = x.Port,
        Username = x.Username,
        NodeId = x.NodeId,
        NodeName = x.Node == null ? null : x.Node.Name,
        MaxDatabases = x.MaxDatabases,
        DatabaseCount = x.Databases.Count,
        CreatedAt = x.CreatedAt
    });
}
