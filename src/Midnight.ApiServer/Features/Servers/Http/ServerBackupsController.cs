using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Http;

[ApiController]
[Route("api/servers/{uuid:guid}/backups")]
[RequirePermission(Permissions.AccountRead)]
public class ServerBackupsController : ControllerBase
{
    private readonly ServerAccessService _access;
    private readonly ServerService _serverService;
    private readonly BackupService _backups;
    private readonly NodeClient _nodeClient;
    private readonly ActivityService _activity;
    private readonly DownloadTicketService _tickets;

    public ServerBackupsController(ServerAccessService access, ServerService serverService, BackupService backups, NodeClient nodeClient,
        ActivityService activity, DownloadTicketService tickets)
    {
        _access = access;
        _serverService = serverService;
        _backups = backups;
        _nodeClient = nodeClient;
        _activity = activity;
        _tickets = tickets;
    }

    [HttpGet]
    public async Task<BackupListResponse> List(Guid uuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.BackupRead, cancellationToken);
        return await _backups.ListAsync(access.Server, cancellationToken);
    }

    /// <summary>409: backups disabled (limit 0), "Backup limit reached", a backup is already running, server suspended/installing</summary>
    [HttpPost]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<BackupResponse> Create(Guid uuid, [FromBody] CreateBackupRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.BackupCreate, cancellationToken);
        var backup = await _backups.CreateAsync(access.Server, request.Name, request.IgnoredFiles, request.IsLocked, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.BackupCreate, Describe(backup));

        return BackupService.ToResponse(backup);
    }

    [HttpGet("{backupUuid:guid}")]
    public async Task<BackupResponse> Get(Guid uuid, Guid backupUuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.BackupRead, cancellationToken);
        return BackupService.ToResponse(await _backups.GetAsync(access.Server, backupUuid, cancellationToken));
    }

    /// <summary>Toggles IsLocked</summary>
    [HttpPost("{backupUuid:guid}/lock")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<BackupResponse> Lock(Guid uuid, Guid backupUuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.BackupDelete, cancellationToken);
        var backup = await _backups.ToggleLockAsync(access.Server, backupUuid, cancellationToken);

        await _activity.LogAsync(access.Server.Id, backup.IsLocked ? ActivityEvents.BackupLock : ActivityEvents.BackupUnlock, Describe(backup));

        return BackupService.ToResponse(backup);
    }

    /// <summary>Single use link (60 s): {"url": "/api/servers/{uuid}/backups/{backupUuid}/download?ticket=…"}</summary>
    [HttpPost("{backupUuid:guid}/download-ticket")]
    public async Task<DownloadTicketResponse> DownloadTicket(Guid uuid, Guid backupUuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.BackupDownload, cancellationToken);
        var backup = await _backups.GetAsync(access.Server, backupUuid, cancellationToken);

        BackupService.EnsureDownloadable(backup);

        var ticket = _tickets.CreateForBackup(access.UserId, uuid, backup.Uuid);

        return new DownloadTicketResponse { Url = $"/api/servers/{uuid}/backups/{backup.Uuid}/download?ticket={ticket}" };
    }

    /// <summary>Streams the tar.gz from the demon, authenticated with the ticket</summary>
    [HttpGet("{backupUuid:guid}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(Guid uuid, Guid backupUuid, [FromQuery] string? ticket, CancellationToken cancellationToken)
    {
        var redeemed = _tickets.Redeem(ticket, uuid, DownloadKind.Backup);

        if (redeemed == null || redeemed.Target != backupUuid.ToString())
            throw HttpApiException.Unauthorized("Invalid or expired download link");

        var server = await _serverService.GetAsync(uuid, cancellationToken);
        var backup = await _backups.GetAsync(server, backupUuid, cancellationToken);

        BackupService.EnsureDownloadable(backup);

        HttpResponseMessage response;

        try
        {
            response = await _nodeClient.DownloadBackupAsync(server.Node, server.Uuid, backup.Uuid, cancellationToken);
        }
        catch (Exception e) when (NodeClient.IsNodeError(e, cancellationToken))
        {
            throw NodeClient.ToProxyApiException(e);
        }

        await _activity.LogAsync(server.Id, ActivityEvents.BackupDownload, Describe(backup),
            new ActivityActor(redeemed.UserId, HttpContext.Connection.RemoteIpAddress?.ToString()));

        return await DemonStreamResult.CreateAsync(HttpContext, response, GetDownloadName(backup.Name), "application/gzip");
    }

    /// <summary>202, the demon stops the server and restores in the background (backup-restore-started / -completed events)</summary>
    [HttpPost("{backupUuid:guid}/restore")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Restore(Guid uuid, Guid backupUuid, [FromBody] RestoreBackupRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.BackupRestore, cancellationToken);
        var backup = await _backups.RestoreAsync(access.Server, backupUuid, request.Truncate, cancellationToken);

        var properties = Describe(backup);
        properties["truncate"] = request.Truncate ? "true" : "false";
        await _activity.LogAsync(access.Server.Id, ActivityEvents.BackupRestore, properties);

        return Accepted();
    }

    /// <summary>409 when locked or still running</summary>
    [HttpDelete("{backupUuid:guid}")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Delete(Guid uuid, Guid backupUuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.BackupDelete, cancellationToken);
        var backup = await _backups.DeleteAsync(access.Server, backupUuid, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.BackupDelete, Describe(backup));

        return NoContent();
    }

    private static Dictionary<string, string?> Describe(Entities.Backup backup) => new()
    {
        ["backup"] = backup.Name,
        ["uuid"] = backup.Uuid.ToString()
    };

    /// <summary>File system safe name with .tar.gz</summary>
    internal static string GetDownloadName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '"']).ToHashSet();
        var safe = new string(name.Select(x => invalid.Contains(x) || char.IsControl(x) ? '_' : x).ToArray()).Trim();

        return (safe.Length == 0 ? "backup" : safe) + ".tar.gz";
    }
}
