using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.Shared;
using Midnight.Shared.Enums;
using Midnight.Shared.Http;
using Midnight.Shared.Protocol;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>
/// Connects the browser websocket with the console websocket of the demon. Messages of the demon are forwarded
/// unchanged, the browser may only send "command" (control.console) and "power" events (control.start / stop / restart)
/// if the permissions in the ticket allow it and the server isn't suspended.
/// </summary>
public class ConsoleProxy
{
    /// <summary>Browser messages are small json objects, a command is at most 2048 characters</summary>
    public const int MaxBrowserMessageSize = 16 * 1024;

    /// <summary>The history event of the demon can be large</summary>
    public const int MaxDemonMessageSize = 4 * 1024 * 1024;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    private readonly DataContext _context;
    private readonly NodeClient _nodeClient;
    private readonly ActivityService _activityService;
    private readonly ILogger<ConsoleProxy> _logger;

    public ConsoleProxy(DataContext context, NodeClient nodeClient, ActivityService activityService, ILogger<ConsoleProxy> logger)
    {
        _context = context;
        _nodeClient = nodeClient;
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>Accepts the browser websocket and proxies it until one side closes</summary>
    public async Task RunAsync(HttpContext httpContext, Server server, ConsoleTicket ticket)
    {
        var actor = new ActivityActor(ticket.UserId, httpContext.Connection.RemoteIpAddress?.ToString());

        var aborted = httpContext.RequestAborted;

        using var browser = await httpContext.WebSockets.AcceptWebSocketAsync();
        using var demon = new ClientWebSocket();
        using var sendLock = new SemaphoreSlim(1, 1);

        demon.Options.SetRequestHeader("Authorization", _nodeClient.GetAuthorizationHeader(server.Node));
        demon.Options.SetRequestHeader(MidnightProtocol.HeaderName, MidnightProtocol.Version.ToString());
        demon.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(aborted);
            timeout.CancelAfter(ConnectTimeout);

            await demon.ConnectAsync(NodeClient.GetWebSocketUri(server.Node, $"/api/servers/{server.Uuid}/ws"), timeout.Token);
        }
        catch (Exception e) when (e is WebSocketException or HttpRequestException or OperationCanceledException && !aborted.IsCancellationRequested)
        {
            _logger.LogDebug("Console of server {ServerUuid}: node {NodeId} is not reachable: {Error}", server.Uuid, server.NodeId, e.Message);

            await SendErrorAsync(browser, sendLock, "The node is not reachable", aborted);
            await CloseAsync(browser, WebSocketCloseStatus.EndpointUnavailable, "Node not reachable");
            return;
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(aborted);

        var demonToBrowser = PumpDemonAsync(demon, browser, sendLock, stop.Token);
        var browserToDemon = PumpBrowserAsync(browser, demon, sendLock, server, ticket.Permissions, actor, stop.Token);

        var first = await Task.WhenAny(demonToBrowser, browserToDemon);

        // One side is done: close the other one cleanly and give it a moment to answer the close frame
        if (first == demonToBrowser)
            await CloseAsync(browser, demon.CloseStatus ?? WebSocketCloseStatus.NormalClosure, demon.CloseStatusDescription);
        else
            await CloseAsync(demon, WebSocketCloseStatus.NormalClosure, "Browser disconnected");

        await Task.WhenAny(Task.WhenAll(demonToBrowser, browserToDemon), Task.Delay(CloseTimeout, CancellationToken.None));
        await stop.CancelAsync();

        try
        {
            await Task.WhenAll(demonToBrowser, browserToDemon);
        }
        catch (Exception e)
        {
            // The response is already a websocket, nothing can be reported anymore. Usually one side just went away.
            if (e is not (OperationCanceledException or WebSocketException))
                _logger.LogWarning(e, "Console proxy of server {ServerUuid} failed", server.Uuid);
        }
    }

    private static async Task PumpDemonAsync(WebSocket demon, WebSocket browser, SemaphoreSlim sendLock, CancellationToken cancellationToken)
    {
        while (demon.State == WebSocketState.Open && browser.State == WebSocketState.Open)
        {
            var message = await ReceiveAsync(demon, MaxDemonMessageSize, cancellationToken);

            if (message == null)
                return;

            await SendAsync(browser, sendLock, message.Value.Data, message.Value.Type, cancellationToken);
        }
    }

    private async Task PumpBrowserAsync(WebSocket browser, WebSocket demon, SemaphoreSlim sendLock, Server server, IReadOnlyList<string> permissions,
        ActivityActor actor, CancellationToken cancellationToken)
    {
        while (browser.State == WebSocketState.Open && demon.State == WebSocketState.Open)
        {
            var message = await ReceiveAsync(browser, MaxBrowserMessageSize, cancellationToken);

            if (message == null)
                return;

            if (message.Value.Type != WebSocketMessageType.Text)
                continue;

            var (error, activity) = await CheckBrowserMessageAsync(message.Value.Data, server.Id, permissions, cancellationToken);

            if (error != null)
            {
                await SendErrorAsync(browser, sendLock, error, cancellationToken);
                continue;
            }

            // Only the proxy writes to the demon socket, no lock needed
            await demon.SendAsync(message.Value.Data, WebSocketMessageType.Text, true, cancellationToken);

            if (activity != null)
                await _activityService.LogAsync(server.Id, activity.Value.Event, activity.Value.Properties, actor);
        }
    }

    /// <summary>Error null if the message may be forwarded to the demon, otherwise the error for the browser</summary>
    private async Task<(string? Error, (string Event, Dictionary<string, string?> Properties)? Activity)> CheckBrowserMessageAsync(
        byte[] data, int serverId, IReadOnlyList<string> permissions, CancellationToken cancellationToken)
    {
        var (error, permission, activity) = ParseBrowserMessage(data);

        if (error != null)
            return (error, null);

        if (!ServerPermissions.Has(permissions, permission!))
            return ($"You need the permission '{permission}' for this", null);

        // Checked for every message: the server may have been suspended while the console is open
        var suspended = await _context.Servers.Where(x => x.Id == serverId).Select(x => (bool?)x.Suspended).FirstOrDefaultAsync(cancellationToken);

        return suspended switch
        {
            null => ("The server no longer exists", null),
            true => ("The server is suspended", null),
            _ => (null, activity)
        };
    }

    /// <summary>Checks the message format and returns the needed permission and the activity entry</summary>
    internal static (string? Error, string? Permission, (string Event, Dictionary<string, string?> Properties)? Activity) ParseBrowserMessage(byte[] data)
    {
        JsonElement root;

        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(data, JsonDefaults.Options);
        }
        catch (JsonException)
        {
            return ("Invalid message", null, null);
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("event", out var eventElement) || eventElement.ValueKind != JsonValueKind.String)
            return ("Invalid message", null, null);

        var eventName = eventElement.GetString();
        var payload = root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.String ? dataElement.GetString() : null;

        switch (eventName)
        {
            case ConsoleEvents.SendCommand:
                if (string.IsNullOrEmpty(payload))
                    return ("Invalid command", null, null);

                return (null, ServerPermissions.ControlConsole, (ActivityEvents.Command, new Dictionary<string, string?> { ["command"] = payload }));

            case ConsoleEvents.SetPower:
                if (!Enum.TryParse<PowerAction>(payload, ignoreCase: true, out var action) || !Enum.IsDefined(action) || int.TryParse(payload, out _))
                    return ("Invalid power action", null, null);

                return (null, ServerAccess.GetPowerPermission(action), (ActivityEvents.Power(action), []));

            default:
                return ($"Unknown event '{eventName}'", null, null);
        }
    }

    /// <summary>Reads one complete message, null when the socket was closed or the message is too large</summary>
    private static async Task<(byte[] Data, WebSocketMessageType Type)?> ReceiveAsync(WebSocket socket, int maxSize, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                // Complete the close handshake
                await CloseAsync(socket, result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription);
                return null;
            }

            if (stream.Length + result.Count > maxSize)
            {
                await CloseAsync(socket, WebSocketCloseStatus.MessageTooBig, "Message too large");
                return null;
            }

            stream.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
                return (stream.ToArray(), result.MessageType);
        }
    }

    private static async Task SendAsync(WebSocket socket, SemaphoreSlim sendLock, byte[] data, WebSocketMessageType type, CancellationToken cancellationToken)
    {
        await sendLock.WaitAsync(cancellationToken);

        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(data, type, true, cancellationToken);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private static Task SendErrorAsync(WebSocket socket, SemaphoreSlim sendLock, string error, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(new ConsoleMessage { Event = ConsoleEvents.Error, Data = error }, JsonDefaults.Options);
        return SendAsync(socket, sendLock, Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, cancellationToken);
    }

    private static async Task CloseAsync(WebSocket socket, WebSocketCloseStatus status, string? description)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            return;

        try
        {
            using var timeout = new CancellationTokenSource(CloseTimeout);
            await socket.CloseOutputAsync(status, description, timeout.Token);
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The other side is already gone
        }
    }
}
