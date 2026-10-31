using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.Shared.Http.Demon;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>
/// Backups are created by the demon in the background, it reports the result to /api/nodes/remote/backups/{uuid}.
/// Limit: FeatureLimits.Backups counts running and successful backups (failed ones don't count), nothing is deleted automatically.
/// </summary>
public class BackupService
{
    private readonly DataContext _context;
    private readonly NodeClient _nodeClient;
    private readonly IOptionsMonitor<AppConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;

    public BackupService(DataContext context, NodeClient nodeClient, IOptionsMonitor<AppConfiguration> configuration, TimeProvider timeProvider)
    {
        _context = context;
        _nodeClient = nodeClient;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<BackupListResponse> ListAsync(Server server, CancellationToken cancellationToken)
    {
        var backups = await _context.Backups
            .AsNoTracking()
            .Where(x => x.ServerId == server.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return new BackupListResponse { Backups = backups.Select(ToResponse).ToList(), Limit = server.BackupLimit };
    }

    public async Task<Backup> GetAsync(Server server, Guid backupUuid, CancellationToken cancellationToken)
        => await _context.Backups.FirstOrDefaultAsync(x => x.Uuid == backupUuid && x.ServerId == server.Id, cancellationToken)
           ?? throw HttpApiException.NotFound("Backup not found");

    /// <summary>409 when backups are disabled, the limit is reached or another backup is running; 502 when the node fails (nothing saved)</summary>
    public async Task<Backup> CreateAsync(Server server, string? name, string? ignoredFiles, bool locked, CancellationToken cancellationToken)
    {
        ServerService.EnsureUsable(server);

        if (server.BackupLimit <= 0)
            throw HttpApiException.Conflict("Backups are disabled", "This server can't have backups");

        var backups = await _context.Backups
            .Where(x => x.ServerId == server.Id && (x.CompletedAt == null || x.IsSuccessful))
            .Select(x => new { x.CompletedAt })
            .ToListAsync(cancellationToken);

        if (backups.Count >= server.BackupLimit)
            throw HttpApiException.Conflict("Backup limit reached", $"This server can have at most {server.BackupLimit} backup(s), delete an old one first");

        if (backups.Any(x => x.CompletedAt == null))
            throw HttpApiException.Conflict("A backup is already running", "Wait until the running backup is finished");

        var now = _timeProvider.GetUtcNow();

        var backup = new Backup
        {
            Uuid = Guid.NewGuid(),
            ServerId = server.Id,
            Name = string.IsNullOrWhiteSpace(name) ? DefaultName(now) : name.Trim(),
            IgnoredFiles = NormalizeIgnoredFiles(ignoredFiles),
            IsLocked = locked,
            CreatedAt = now
        };

        // Saved before the demon is called: small backups can be reported back before the call returns
        _context.Backups.Add(backup);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _nodeClient.CreateBackupAsync(server.Node, server.Uuid, new DemonCreateBackupRequest
            {
                BackupUuid = backup.Uuid,
                IgnoredFiles = backup.IgnoredFiles
            }, cancellationToken);
        }
        catch (Exception e) when (NodeClient.IsNodeError(e, cancellationToken))
        {
            _context.Backups.Remove(backup);
            await _context.SaveChangesAsync(CancellationToken.None);

            throw NodeClient.ToApiException(e);
        }

        return backup;
    }

    public async Task<Backup> ToggleLockAsync(Server server, Guid backupUuid, CancellationToken cancellationToken)
    {
        var backup = await GetAsync(server, backupUuid, cancellationToken);

        backup.IsLocked = !backup.IsLocked;
        await _context.SaveChangesAsync(cancellationToken);

        return backup;
    }

    /// <summary>The demon stops the server, restores in the background and reports the result</summary>
    public async Task<Backup> RestoreAsync(Server server, Guid backupUuid, bool truncate, CancellationToken cancellationToken)
    {
        var backup = await GetAsync(server, backupUuid, cancellationToken);

        ServerService.EnsureUsable(server);
        EnsureDownloadable(backup);

        await ServerService.CallNodeAsync(() => _nodeClient.RestoreBackupAsync(server.Node, server.Uuid, backup.Uuid,
            new DemonRestoreBackupRequest { Truncate = truncate }, cancellationToken), cancellationToken);

        return backup;
    }

    /// <summary>Locked or running backups can't be deleted (409). The file is deleted on the demon first (502 on failure, 404 is fine).</summary>
    public async Task<Backup> DeleteAsync(Server server, Guid backupUuid, CancellationToken cancellationToken)
    {
        var backup = await GetAsync(server, backupUuid, cancellationToken);

        if (backup.IsLocked)
            throw HttpApiException.Conflict("The backup is locked", "Unlock the backup before deleting it");

        if (backup.CompletedAt == null)
            throw HttpApiException.Conflict("The backup is still running");

        await ServerService.CallNodeAsync(() => _nodeClient.DeleteBackupAsync(server.Node, server.Uuid, backup.Uuid, cancellationToken), cancellationToken);

        _context.Backups.Remove(backup);
        await _context.SaveChangesAsync(cancellationToken);

        return backup;
    }

    /// <summary>Only completed, successful backups can be downloaded and restored</summary>
    public static void EnsureDownloadable(Backup backup)
    {
        if (backup.CompletedAt == null)
            throw HttpApiException.Conflict("The backup is still running");

        if (!backup.IsSuccessful)
            throw HttpApiException.Conflict("The backup failed");
    }

    // ─── Demon results ──────────────────────────────────────────────────────

    /// <summary>Backup of a server on the calling node, 404 for every other backup</summary>
    public async Task<Backup> GetForNodeAsync(int nodeId, Guid backupUuid, CancellationToken cancellationToken)
        => await _context.Backups.Include(x => x.Server).FirstOrDefaultAsync(x => x.Uuid == backupUuid && x.Server.NodeId == nodeId, cancellationToken)
           ?? throw HttpApiException.NotFound("Backup not found");

    public async Task<Backup> SetResultAsync(int nodeId, Guid backupUuid, DemonBackupResultRequest request, CancellationToken cancellationToken)
    {
        var backup = await GetForNodeAsync(nodeId, backupUuid, cancellationToken);

        backup.IsSuccessful = request.Successful;
        backup.Checksum = request.Successful ? Truncate(request.Checksum, 128) : null;
        backup.Bytes = Math.Max(0, request.Bytes);
        backup.CompletedAt = _timeProvider.GetUtcNow();

        await _context.SaveChangesAsync(cancellationToken);

        return backup;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private string DefaultName(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, PanelTimeZone.Get(_configuration.CurrentValue.TimeZone));
        return $"Backup at {local:yyyy-MM-dd HH:mm}";
    }

    /// <summary>Trims the lines and drops empty ones, CRLF becomes LF</summary>
    public static string NormalizeIgnoredFiles(string? ignoredFiles)
        => string.Join('\n', (ignoredFiles ?? "")
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => x.Length > 0));

    private static string? Truncate(string? value, int length)
        => value == null || value.Length <= length ? value : value[..length];

    public static BackupResponse ToResponse(Backup backup) => new()
    {
        Uuid = backup.Uuid,
        Name = backup.Name,
        IgnoredFiles = backup.IgnoredFiles,
        Checksum = backup.Checksum,
        Bytes = backup.Bytes,
        IsSuccessful = backup.IsSuccessful,
        IsLocked = backup.IsLocked,
        CompletedAt = backup.CompletedAt,
        CreatedAt = backup.CreatedAt
    };
}

/// <summary>The configured panel time zone (TimeZone in config.json), UTC if it is unknown</summary>
public static class PanelTimeZone
{
    public static TimeZoneInfo Get(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TimeZoneInfo.Utc;

        return TimeZoneInfo.TryFindSystemTimeZoneById(id.Trim(), out var zone) ? zone : TimeZoneInfo.Utc;
    }
}
