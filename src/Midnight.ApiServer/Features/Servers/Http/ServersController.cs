using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Http;

/// <summary>
/// Servers of the current user: owned ones and ones they are a subuser of. Every endpoint checks the effective server
/// permissions (see ServerAccess), users without access get 404. Api keys need account.read / account.write scopes.
/// </summary>
[ApiController]
[Route("api/servers")]
[RequirePermission(Permissions.AccountRead)]
public class ServersController : ControllerBase
{
    private readonly ServerService _serverService;
    private readonly ServerAccessService _access;
    private readonly ActivityService _activity;
    private readonly ConsoleTicketService _ticketService;
    private readonly ConsoleProxy _consoleProxy;
    private readonly ILogger<ServersController> _logger;

    public ServersController(ServerService serverService, ServerAccessService access, ActivityService activity, ConsoleTicketService ticketService,
        ConsoleProxy consoleProxy, ILogger<ServersController> logger)
    {
        _serverService = serverService;
        _access = access;
        _activity = activity;
        _ticketService = ticketService;
        _consoleProxy = consoleProxy;
        _logger = logger;
    }

    /// <summary>Own servers and servers the user is a subuser of, all=true lists every server (admin.servers.read)</summary>
    [HttpGet]
    public async Task<List<ServerSummaryResponse>> List([FromQuery] bool all = false, CancellationToken cancellationToken = default)
    {
        EnsureCanListAll(all);

        var userId = User.GetUserId();
        var servers = await _serverService.ListForUserAsync(userId, all, cancellationToken);

        return servers.Select(x => ServerService.ToSummary(x, userId)).ToList();
    }

    /// <summary>The server permissions with descriptions, for the subuser editor</summary>
    [HttpGet("permissions")]
    public ServerPermissionGroup[] PermissionGroups() => ServerPermissions.Groups;

    /// <summary>Live state of the listed servers (dashboard), one request per node</summary>
    [HttpGet("resources")]
    public async Task<Dictionary<Guid, ServerResourcesResponse>> Resources([FromQuery] bool all = false, CancellationToken cancellationToken = default)
    {
        EnsureCanListAll(all);

        var servers = await _serverService.ListForUserAsync(User.GetUserId(), all, cancellationToken);
        return await _serverService.GetResourcesAsync(servers, cancellationToken);
    }

    /// <summary>Any access. Variables are only included with startup.read.</summary>
    [HttpGet("{uuid:guid}")]
    public async Task<ServerDetailResponse> Get(Guid uuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, null, cancellationToken);
        return ServerService.ToDetail(access.Server, access.UserId, access.Permissions);
    }

    [HttpGet("{uuid:guid}/resources")]
    public async Task<ServerResourcesResponse> Resources(Guid uuid, CancellationToken cancellationToken)
        => await _serverService.GetResourcesAsync((await _access.GetAsync(User, uuid, null, cancellationToken)).Server, cancellationToken);

    /// <summary>control.start / control.stop (stop and kill) / control.restart</summary>
    [HttpPost("{uuid:guid}/power")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Power(Guid uuid, [FromBody] PowerRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerAccess.GetPowerPermission(request.Action), cancellationToken);

        await _serverService.PowerAsync(access.Server, request.Action, cancellationToken);
        await _activity.LogAsync(access.Server.Id, ActivityEvents.Power(request.Action));

        return NoContent();
    }

    [HttpPost("{uuid:guid}/command")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Command(Guid uuid, [FromBody] CommandRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ControlConsole, cancellationToken);

        await _serverService.SendCommandAsync(access.Server, request.Command, cancellationToken);
        await _activity.LogAsync(access.Server.Id, ActivityEvents.Command, new Dictionary<string, string?> { ["command"] = request.Command });

        return NoContent();
    }

    [HttpPut("{uuid:guid}/rename")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ServerDetailResponse> Rename(Guid uuid, [FromBody] RenameServerRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.SettingsRename, cancellationToken);
        var oldName = access.Server.Name;

        await _serverService.RenameAsync(access.Server, request, cancellationToken);
        await _activity.LogAsync(access.Server.Id, ActivityEvents.Rename, new Dictionary<string, string?> { ["old"] = oldName, ["new"] = access.Server.Name });

        return ServerService.ToDetail(access.Server, access.UserId, access.Permissions);
    }

    /// <summary>Only variables the user may edit, the rules of the shard are validated</summary>
    [HttpPut("{uuid:guid}/variables")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ServerDetailResponse> UpdateVariables(Guid uuid, [FromBody] UpdateServerVariablesRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.StartupUpdate, cancellationToken);

        await _serverService.UpdateUserVariablesAsync(access.Server, request, cancellationToken);
        await _activity.LogAsync(access.Server.Id, ActivityEvents.StartupUpdate, new Dictionary<string, string?>
        {
            ["variables"] = string.Join(", ", request.Variables.Keys)
        });

        return ServerService.ToDetail(access.Server, access.UserId, access.Permissions);
    }

    [HttpPost("{uuid:guid}/reinstall")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Reinstall(Guid uuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.SettingsReinstall, cancellationToken);

        await _serverService.ReinstallAsync(access.Server, cancellationToken);
        await _activity.LogAsync(access.Server.Id, ActivityEvents.Reinstall);
        _logger.LogInformation("User {ActorId} reinstalled server {ServerUuid}", User.GetUserId(), uuid);

        return NoContent();
    }

    /// <summary>Single use ticket for the console websocket (websocket.connect), valid for 30 seconds</summary>
    [HttpPost("{uuid:guid}/console-ticket")]
    public async Task<ConsoleTicketResponse> ConsoleTicket(Guid uuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.WebsocketConnect, cancellationToken);

        // The ticket carries the effective permissions, "command" and "power" events are checked against them
        var ticket = _ticketService.Create(access.UserId, uuid, access.Permissions);

        return new ConsoleTicketResponse
        {
            Ticket = ticket,
            Path = $"/api/servers/{uuid}/console?ticket={ticket}"
        };
    }

    /// <summary>Console websocket, authenticated with a ticket because browsers can't send headers on websockets</summary>
    [HttpGet("{uuid:guid}/console")]
    [AllowAnonymous]
    public async Task ConsoleSocket(Guid uuid, [FromQuery] string? ticket, CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            throw HttpApiException.BadRequest("Websocket request expected");

        var redeemed = _ticketService.Redeem(ticket, uuid)
                       ?? throw HttpApiException.Unauthorized("Invalid or expired console ticket");

        var server = await _serverService.GetAsync(uuid, cancellationToken);

        _logger.LogDebug("User {UserId} opened the console of server {ServerUuid}", redeemed.UserId, uuid);

        await _consoleProxy.RunAsync(HttpContext, server, redeemed);
    }

    private void EnsureCanListAll(bool all)
    {
        if (all && !User.HasPermission(Permissions.AdminServersRead))
            throw HttpApiException.Forbidden("Listing all servers requires the permission " + Permissions.AdminServersRead);
    }
}
