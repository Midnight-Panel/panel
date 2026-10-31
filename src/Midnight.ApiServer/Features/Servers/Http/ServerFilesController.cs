using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Http;

/// <summary>
/// File manager, every call is forwarded 1:1 to the demon (same paths, query strings and bodies). The demon keeps all paths
/// inside the server volume. Demon client errors (400, 403, 404, 409, 413, …) keep their status code, unreachable nodes give 502.
/// Suspended or installing servers: 409.
/// </summary>
[ApiController]
[Route("api/servers/{uuid:guid}/files")]
[RequirePermission(Permissions.AccountRead)]
public class ServerFilesController : ControllerBase
{
    private const int MaxPathLength = 4096;

    private readonly ServerAccessService _access;
    private readonly ServerService _serverService;
    private readonly NodeClient _nodeClient;
    private readonly ActivityService _activity;
    private readonly DownloadTicketService _tickets;
    private readonly IOptionsMonitor<AppConfiguration> _configuration;

    public ServerFilesController(ServerAccessService access, ServerService serverService, NodeClient nodeClient, ActivityService activity,
        DownloadTicketService tickets, IOptionsMonitor<AppConfiguration> configuration)
    {
        _access = access;
        _serverService = serverService;
        _nodeClient = nodeClient;
        _activity = activity;
        _tickets = tickets;
        _configuration = configuration;
    }

    [HttpGet("list")]
    public async Task<List<FileEntryResponse>> List(Guid uuid, [FromQuery] string? directory, CancellationToken cancellationToken)
    {
        var server = await GetServerAsync(uuid, ServerPermissions.FileRead, cancellationToken);
        var path = RequirePath(directory ?? "/", "directory");

        return await ProxyAsync(() => _nodeClient.ListFilesAsync(server.Node, server.Uuid, path, cancellationToken), cancellationToken);
    }

    /// <summary>text/plain body. 413 when larger than 5 MiB, 400 for binary files (decided by the demon).</summary>
    [HttpGet("contents")]
    [Produces("text/plain")]
    public async Task<IActionResult> Contents(Guid uuid, [FromQuery] string? file, CancellationToken cancellationToken)
    {
        var server = await GetServerAsync(uuid, ServerPermissions.FileReadContent, cancellationToken);
        var path = RequirePath(file, "file");

        var response = await ProxyAsync(() => _nodeClient.GetFileContentsAsync(server.Node, server.Uuid, path, cancellationToken), cancellationToken);

        return await DemonStreamResult.CreateAsync(HttpContext, response, null, "text/plain; charset=utf-8");
    }

    /// <summary>Raw text body. Needs file.update, or file.create if the file doesn't exist yet.</summary>
    [HttpPost("write")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Write(Guid uuid, [FromQuery] string? file, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, null, cancellationToken);
        var server = access.Server;
        var path = RequirePath(file, "file");

        ServerService.EnsureUsable(server);

        if (!access.Has(ServerPermissions.FileUpdate))
        {
            // Without file.update only new files may be written
            access.Ensure(ServerPermissions.FileCreate);

            if (await FileExistsAsync(server, path, cancellationToken))
                access.Ensure(ServerPermissions.FileUpdate);
        }

        var content = new StreamContent(Request.Body);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") { CharSet = "utf-8" };

        if (Request.ContentLength is { } length)
            content.Headers.ContentLength = length;

        await ProxyAsync(async () => { await _nodeClient.WriteFileAsync(server.Node, server.Uuid, path, content, cancellationToken); return 0; }, cancellationToken);
        await _activity.LogAsync(server.Id, ActivityEvents.FileWrite, new Dictionary<string, string?> { ["file"] = path });

        return NoContent();
    }

    [HttpPost("create-folder")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> CreateFolder(Guid uuid, [FromBody] CreateFolderRequest request, CancellationToken cancellationToken)
    {
        var server = await GetServerAsync(uuid, ServerPermissions.FileCreate, cancellationToken);

        await ProxyAsync(async () => { await _nodeClient.CreateFolderAsync(server.Node, server.Uuid, request, cancellationToken); return 0; }, cancellationToken);
        await _activity.LogAsync(server.Id, ActivityEvents.FileCreateFolder, new Dictionary<string, string?> { ["directory"] = request.Root, ["name"] = request.Name });

        return NoContent();
    }

    [HttpPut("rename")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Rename(Guid uuid, [FromBody] RenameFilesRequest request, CancellationToken cancellationToken)
    {
        var server = await GetServerAsync(uuid, ServerPermissions.FileUpdate, cancellationToken);

        await ProxyAsync(async () => { await _nodeClient.RenameFilesAsync(server.Node, server.Uuid, request, cancellationToken); return 0; }, cancellationToken);
        await _activity.LogAsync(server.Id, ActivityEvents.FileRename, new Dictionary<string, string?>
        {
            ["directory"] = request.Root,
            ["files"] = string.Join(", ", request.Files.Select(x => $"{x.From} → {x.To}"))
        });

        return NoContent();
    }

    [HttpPost("copy")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Copy(Guid uuid, [FromBody] CopyFileRequest request, CancellationToken cancellationToken)
    {
        var server = await GetServerAsync(uuid, ServerPermissions.FileCreate, cancellationToken);

        await ProxyAsync(async () => { await _nodeClient.CopyFileAsync(server.Node, server.Uuid, request, cancellationToken); return 0; }, cancellationToken);
        await _activity.LogAsync(server.Id, ActivityEvents.FileCopy, new Dictionary<string, string?> { ["file"] = request.Location });

        return NoContent();
    }

    [HttpPost("delete")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Delete(Guid uuid, [FromBody] DeleteFilesRequest request, CancellationToken cancellationToken)
    {
        var server = await GetServerAsync(uuid, ServerPermissions.FileDelete, cancellationToken);

        await ProxyAsync(async () => { await _nodeClient.DeleteFilesAsync(server.Node, server.Uuid, request, cancellationToken); return 0; }, cancellationToken);
        await _activity.LogAsync(server.Id, ActivityEvents.FileDelete, new Dictionary<string, string?>
        {
            ["directory"] = request.Root,
            ["files"] = string.Join(", ", request.Files)
        });

        return NoContent();
    }

    [HttpPost("compress")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<FileEntryResponse> Compress(Guid uuid, [FromBody] CompressFilesRequest request, CancellationToken cancellationToken)
    {
        var server = await GetServerAsync(uuid, ServerPermissions.FileArchive, cancellationToken);

        var archive = await ProxyAsync(() => _nodeClient.CompressFilesAsync(server.Node, server.Uuid, request, cancellationToken), cancellationToken);
        await _activity.LogAsync(server.Id, ActivityEvents.FileCompress, new Dictionary<string, string?>
        {
            ["directory"] = request.Root,
            ["files"] = string.Join(", ", request.Files),
            ["archive"] = archive.Name
        });

        return archive;
    }

    [HttpPost("decompress")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Decompress(Guid uuid, [FromBody] DecompressFileRequest request, CancellationToken cancellationToken)
    {
        var server = await GetServerAsync(uuid, ServerPermissions.FileArchive, cancellationToken);

        await ProxyAsync(async () => { await _nodeClient.DecompressFileAsync(server.Node, server.Uuid, request, cancellationToken); return 0; }, cancellationToken);
        await _activity.LogAsync(server.Id, ActivityEvents.FileDecompress, new Dictionary<string, string?> { ["directory"] = request.Root, ["file"] = request.File });

        return NoContent();
    }

    [HttpPost("chmod")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Chmod(Guid uuid, [FromBody] ChmodFilesRequest request, CancellationToken cancellationToken)
    {
        var server = await GetServerAsync(uuid, ServerPermissions.FileUpdate, cancellationToken);

        await ProxyAsync(async () => { await _nodeClient.ChmodFilesAsync(server.Node, server.Uuid, request, cancellationToken); return 0; }, cancellationToken);
        await _activity.LogAsync(server.Id, ActivityEvents.FileChmod, new Dictionary<string, string?>
        {
            ["directory"] = request.Root,
            ["files"] = string.Join(", ", request.Files.Select(x => $"{x.File} {x.Mode}"))
        });

        return NoContent();
    }

    /// <summary>The demon downloads the url in the background (202), a notice event reports the result</summary>
    [HttpPost("pull")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Pull(Guid uuid, [FromBody] PullFileRequest request, CancellationToken cancellationToken)
    {
        var server = await GetServerAsync(uuid, ServerPermissions.FileCreate, cancellationToken);

        await ProxyAsync(async () => { await _nodeClient.PullFileAsync(server.Node, server.Uuid, request, cancellationToken); return 0; }, cancellationToken);
        await _activity.LogAsync(server.Id, ActivityEvents.FilePull, new Dictionary<string, string?>
        {
            ["url"] = request.Url,
            ["directory"] = request.Directory,
            ["file"] = request.FileName
        });

        return Accepted();
    }

    /// <summary>
    /// multipart/form-data with one or more "files" parts, streamed to the demon without buffering.
    /// Size limit Servers.MaxUploadBytes (default 1 GiB) → 413.
    /// </summary>
    [HttpPost("upload")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Upload(Guid uuid, [FromQuery] string? directory, CancellationToken cancellationToken)
    {
        var limit = _configuration.CurrentValue.Servers.MaxUploadBytes;

        // The body is not read before this point (no form binding), so the limit can still be set
        var sizeFeature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (sizeFeature is { IsReadOnly: false })
            sizeFeature.MaxRequestBodySize = limit;

        if (Request.ContentLength > limit)
            throw new HttpApiException(StatusCodes.Status413PayloadTooLarge, "Upload too large", $"Uploads may be at most {limit / 1024 / 1024} MiB");

        if (Request.ContentType == null || !Request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) ||
            !Request.ContentType.Contains("boundary=", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpApiException(StatusCodes.Status415UnsupportedMediaType, "multipart/form-data expected");
        }

        var server = await GetServerAsync(uuid, ServerPermissions.FileCreate, cancellationToken);
        var path = RequirePath(directory ?? "/", "directory");

        // The original content type carries the multipart boundary
        var content = new StreamContent(Request.Body);
        content.Headers.TryAddWithoutValidation("Content-Type", Request.ContentType);

        if (Request.ContentLength is { } length)
            content.Headers.ContentLength = length;

        try
        {
            await _nodeClient.UploadFilesAsync(server.Node, server.Uuid, path, content, cancellationToken);
        }
        catch (Exception e) when (IsBodyTooLarge(e))
        {
            // Chunked uploads without Content-Length hit the limit while streaming
            throw new HttpApiException(StatusCodes.Status413PayloadTooLarge, "Upload too large", $"Uploads may be at most {limit / 1024 / 1024} MiB");
        }
        catch (Exception e) when (NodeClient.IsNodeError(e, cancellationToken))
        {
            throw NodeClient.ToProxyApiException(e);
        }

        await _activity.LogAsync(server.Id, ActivityEvents.FileUpload, new Dictionary<string, string?> { ["directory"] = path });

        return NoContent();
    }

    /// <summary>Single use link for the browser (60 s): {"url": "/api/servers/{uuid}/files/download?ticket=…"}</summary>
    [HttpPost("download-ticket")]
    public async Task<DownloadTicketResponse> DownloadTicket(Guid uuid, [FromQuery] string? file, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.FileReadContent, cancellationToken);
        ServerService.EnsureUsable(access.Server);

        var ticket = _tickets.CreateForFile(access.UserId, uuid, RequirePath(file, "file"));

        return new DownloadTicketResponse { Url = $"/api/servers/{uuid}/files/download?ticket={ticket}" };
    }

    /// <summary>Streams the file as attachment. Either ?ticket= (browser links) or ?file= with the normal authentication.</summary>
    [HttpGet("download")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(Guid uuid, [FromQuery] string? file, [FromQuery] string? ticket, CancellationToken cancellationToken)
    {
        Server server;
        string path;
        ActivityActor actor;

        if (ticket != null)
        {
            var redeemed = _tickets.Redeem(ticket, uuid, DownloadKind.File)
                           ?? throw HttpApiException.Unauthorized("Invalid or expired download link");

            server = await _serverService.GetAsync(uuid, cancellationToken);
            path = redeemed.Target;
            actor = new ActivityActor(redeemed.UserId, HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        else
        {
            if (!User.HasPermission(Permissions.AccountRead))
                throw HttpApiException.Unauthorized("Authentication required");

            server = (await _access.GetAsync(User, uuid, ServerPermissions.FileReadContent, cancellationToken)).Server;
            path = RequirePath(file, "file");
            actor = _activity.GetCurrentActor();
        }

        ServerService.EnsureUsable(server);

        var response = await ProxyAsync(() => _nodeClient.DownloadFileAsync(server.Node, server.Uuid, path, cancellationToken), cancellationToken);
        await _activity.LogAsync(server.Id, ActivityEvents.FileDownload, new Dictionary<string, string?> { ["file"] = path }, actor);

        return await DemonStreamResult.CreateAsync(HttpContext, response, DemonStreamResult.GetFileName(path), "application/octet-stream");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<Server> GetServerAsync(Guid uuid, string permission, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, permission, cancellationToken);
        ServerService.EnsureUsable(access.Server);
        return access.Server;
    }

    private async Task<bool> FileExistsAsync(Server server, string path, CancellationToken cancellationToken)
    {
        var normalized = "/" + path.Replace('\\', '/').Trim('/');
        var separator = normalized.LastIndexOf('/');
        var directory = separator <= 0 ? "/" : normalized[..separator];
        var name = normalized[(separator + 1)..];

        try
        {
            var entries = await _nodeClient.ListFilesAsync(server.Node, server.Uuid, directory, cancellationToken);
            return entries.Any(x => x.Name == name);
        }
        catch (NodeException e) when (e.StatusCode == StatusCodes.Status404NotFound)
        {
            // The directory doesn't exist either
            return false;
        }
        catch (Exception e) when (NodeClient.IsNodeError(e, cancellationToken))
        {
            throw NodeClient.ToProxyApiException(e);
        }
    }

    private static bool IsBodyTooLarge(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
                return true;
        }

        return false;
    }

    private static string RequirePath(string? path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw HttpApiException.BadRequest($"The query parameter '{name}' is required");

        if (path.Length > MaxPathLength || path.Contains('\0'))
            throw HttpApiException.BadRequest($"Invalid {name}");

        return path;
    }

    private static async Task<T> ProxyAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (Exception e) when (NodeClient.IsNodeError(e, cancellationToken))
        {
            throw NodeClient.ToProxyApiException(e);
        }
    }
}
