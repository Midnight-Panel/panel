using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.Shared.Enums;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Demon;
using Midnight.Shared.Http.Nodes;
using Midnight.Shared.Http.Servers;
using Midnight.Shared.Protocol;

namespace Midnight.ApiServer.Features.Nodes.Services;

/// <summary>Calls the demon api of a node with its node token</summary>
public class NodeClient
{
    public const string HttpClientName = "midnight-demon";

    /// <summary>Client without overall timeout for uploads, downloads and long running file operations (connect timeout only)</summary>
    public const string StreamingHttpClientName = "midnight-demon-stream";

    private static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ArchiveTimeout = TimeSpan.FromMinutes(15);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NodeService _nodeService;
    private readonly ILogger<NodeClient> _logger;

    public NodeClient(IHttpClientFactory httpClientFactory, NodeService nodeService, ILogger<NodeClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _nodeService = nodeService;
        _logger = logger;
    }

    // UriBuilder adds the brackets ipv6 addresses need, string concatenation would throw for "::1"
    public static Uri GetBaseUri(Node node)
        => new UriBuilder(node.UseSsl ? "https" : "http", node.Fqdn, node.HttpPort, "/").Uri;

    /// <summary>Never throws for connection problems, they are reported in the response</summary>
    public async Task<NodeStatusResponse> GetStatusAsync(Node node, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var system = await GetAsync<SystemInfoResponse>(node, "api/system/info", cancellationToken);

            return new NodeStatusResponse { Online = true, LatencyMs = (int)stopwatch.ElapsedMilliseconds, System = system };
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or NodeException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            _logger.LogDebug(e, "Node {NodeId} is not reachable", node.Id);
            return new NodeStatusResponse { Online = false, Error = Describe(e) };
        }
    }

    public Task<DemonLogsResponse> GetLogsAsync(Node node, int lines, CancellationToken cancellationToken)
        => GetAsync<DemonLogsResponse>(node, $"api/system/logs?lines={lines}", cancellationToken);

    // ─── Servers ────────────────────────────────────────────────────────────

    public Task<List<DemonServerStateResponse>> ListServersAsync(Node node, CancellationToken cancellationToken)
        => GetAsync<List<DemonServerStateResponse>>(node, "api/servers", cancellationToken);

    public Task<DemonServerStateResponse> GetServerAsync(Node node, Guid uuid, CancellationToken cancellationToken)
        => GetAsync<DemonServerStateResponse>(node, $"api/servers/{uuid}", cancellationToken);

    public Task<DemonConsoleLogResponse> GetServerLogsAsync(Node node, Guid uuid, int lines, CancellationToken cancellationToken)
        => GetAsync<DemonConsoleLogResponse>(node, $"api/servers/{uuid}/logs?lines={lines}", cancellationToken);

    /// <summary>Creates the server on the demon, the installation runs in the background</summary>
    public Task CreateServerAsync(Node node, DemonCreateServerRequest request, CancellationToken cancellationToken)
        => SendAsync(node, HttpMethod.Post, "api/servers", request, cancellationToken);

    /// <summary>Replaces the configuration of a server, the demon applies it on the next start</summary>
    public Task UpdateServerAsync(Node node, DemonServerConfiguration configuration, CancellationToken cancellationToken)
        => SendAsync(node, HttpMethod.Put, $"api/servers/{configuration.Uuid}", configuration, cancellationToken);

    /// <summary>Kills and removes the container and deletes the files. Servers the demon doesn't know count as deleted.</summary>
    public async Task DeleteServerAsync(Node node, Guid uuid, CancellationToken cancellationToken)
    {
        try
        {
            await SendAsync(node, HttpMethod.Delete, $"api/servers/{uuid}", null, cancellationToken);
        }
        catch (NodeException e) when (e.StatusCode == StatusCodes.Status404NotFound)
        {
            // Already gone
        }
    }

    public Task ReinstallServerAsync(Node node, Guid uuid, CancellationToken cancellationToken)
        => SendAsync(node, HttpMethod.Post, $"api/servers/{uuid}/reinstall", null, cancellationToken);

    public Task PowerAsync(Node node, Guid uuid, PowerAction action, CancellationToken cancellationToken)
        => SendAsync(node, HttpMethod.Post, $"api/servers/{uuid}/power", new PowerRequest { Action = action }, cancellationToken);

    public Task SendCommandAsync(Node node, Guid uuid, string command, CancellationToken cancellationToken)
        => SendAsync(node, HttpMethod.Post, $"api/servers/{uuid}/commands", new CommandRequest { Command = command }, cancellationToken);

    // ─── Files: same paths, query strings and bodies as the panel endpoints ─────

    public Task<List<FileEntryResponse>> ListFilesAsync(Node node, Guid uuid, string directory, CancellationToken cancellationToken)
        => SendLongAsync<List<FileEntryResponse>>(node, HttpMethod.Get, FilesPath(uuid, "list", "directory", directory), null, FileTimeout, cancellationToken);

    /// <summary>text/plain response of the demon, the caller streams and disposes it</summary>
    public Task<HttpResponseMessage> GetFileContentsAsync(Node node, Guid uuid, string file, CancellationToken cancellationToken)
        => SendStreamAsync(node, HttpMethod.Get, FilesPath(uuid, "contents", "file", file), null, cancellationToken);

    /// <summary>Raw file download, the caller streams and disposes the response</summary>
    public Task<HttpResponseMessage> DownloadFileAsync(Node node, Guid uuid, string file, CancellationToken cancellationToken)
        => SendStreamAsync(node, HttpMethod.Get, FilesPath(uuid, "download", "file", file), null, cancellationToken);

    /// <summary>Writes the text body (streamed) to the file</summary>
    public async Task WriteFileAsync(Node node, Guid uuid, string file, HttpContent content, CancellationToken cancellationToken)
    {
        using var response = await SendStreamAsync(node, HttpMethod.Post, FilesPath(uuid, "write", "file", file), content, cancellationToken);
    }

    /// <summary>Forwards a multipart/form-data body without buffering it</summary>
    public async Task UploadFilesAsync(Node node, Guid uuid, string directory, HttpContent content, CancellationToken cancellationToken)
    {
        using var response = await SendStreamAsync(node, HttpMethod.Post, FilesPath(uuid, "upload", "directory", directory), content, cancellationToken);
    }

    public Task CreateFolderAsync(Node node, Guid uuid, CreateFolderRequest request, CancellationToken cancellationToken)
        => SendLongAsync<object>(node, HttpMethod.Post, FilesPath(uuid, "create-folder"), request, FileTimeout, cancellationToken, readBody: false);

    public Task RenameFilesAsync(Node node, Guid uuid, RenameFilesRequest request, CancellationToken cancellationToken)
        => SendLongAsync<object>(node, HttpMethod.Put, FilesPath(uuid, "rename"), request, FileTimeout, cancellationToken, readBody: false);

    public Task CopyFileAsync(Node node, Guid uuid, CopyFileRequest request, CancellationToken cancellationToken)
        => SendLongAsync<object>(node, HttpMethod.Post, FilesPath(uuid, "copy"), request, ArchiveTimeout, cancellationToken, readBody: false);

    public Task DeleteFilesAsync(Node node, Guid uuid, DeleteFilesRequest request, CancellationToken cancellationToken)
        => SendLongAsync<object>(node, HttpMethod.Post, FilesPath(uuid, "delete"), request, ArchiveTimeout, cancellationToken, readBody: false);

    public Task<FileEntryResponse> CompressFilesAsync(Node node, Guid uuid, CompressFilesRequest request, CancellationToken cancellationToken)
        => SendLongAsync<FileEntryResponse>(node, HttpMethod.Post, FilesPath(uuid, "compress"), request, ArchiveTimeout, cancellationToken);

    public Task DecompressFileAsync(Node node, Guid uuid, DecompressFileRequest request, CancellationToken cancellationToken)
        => SendLongAsync<object>(node, HttpMethod.Post, FilesPath(uuid, "decompress"), request, ArchiveTimeout, cancellationToken, readBody: false);

    public Task ChmodFilesAsync(Node node, Guid uuid, ChmodFilesRequest request, CancellationToken cancellationToken)
        => SendLongAsync<object>(node, HttpMethod.Post, FilesPath(uuid, "chmod"), request, FileTimeout, cancellationToken, readBody: false);

    /// <summary>The demon downloads the file in the background (202)</summary>
    public Task PullFileAsync(Node node, Guid uuid, PullFileRequest request, CancellationToken cancellationToken)
        => SendLongAsync<object>(node, HttpMethod.Post, FilesPath(uuid, "pull"), request, FileTimeout, cancellationToken, readBody: false);

    /// <summary>api/servers/{uuid}/files/{action}?{key}={escaped value}</summary>
    internal static string FilesPath(Guid uuid, string action, string? key = null, string? value = null)
        => key == null
            ? $"api/servers/{uuid}/files/{action}"
            : $"api/servers/{uuid}/files/{action}?{key}={Uri.EscapeDataString(value ?? "")}";

    // ─── Backups ────────────────────────────────────────────────────────────

    /// <summary>The demon creates the archive in the background and reports to /api/nodes/remote/backups/{backupUuid}</summary>
    public Task CreateBackupAsync(Node node, Guid uuid, DemonCreateBackupRequest request, CancellationToken cancellationToken)
        => SendAsync(node, HttpMethod.Post, $"api/servers/{uuid}/backups", request, cancellationToken);

    public Task RestoreBackupAsync(Node node, Guid uuid, Guid backupUuid, DemonRestoreBackupRequest request, CancellationToken cancellationToken)
        => SendAsync(node, HttpMethod.Post, $"api/servers/{uuid}/backups/{backupUuid}/restore", request, cancellationToken);

    /// <summary>Backups the demon doesn't know count as deleted</summary>
    public async Task DeleteBackupAsync(Node node, Guid uuid, Guid backupUuid, CancellationToken cancellationToken)
    {
        try
        {
            await SendLongAsync<object>(node, HttpMethod.Delete, $"api/servers/{uuid}/backups/{backupUuid}", null, FileTimeout, cancellationToken, readBody: false);
        }
        catch (NodeException e) when (e.StatusCode == StatusCodes.Status404NotFound)
        {
            // Already gone
        }
    }

    public Task<HttpResponseMessage> DownloadBackupAsync(Node node, Guid uuid, Guid backupUuid, CancellationToken cancellationToken)
        => SendStreamAsync(node, HttpMethod.Get, $"api/servers/{uuid}/backups/{backupUuid}/download", null, cancellationToken);

    // ─── Websocket ──────────────────────────────────────────────────────────

    /// <summary>ws:// or wss:// uri of a demon endpoint</summary>
    public static Uri GetWebSocketUri(Node node, string path)
        => new UriBuilder(node.UseSsl ? "wss" : "ws", node.Fqdn, node.HttpPort, path).Uri;

    /// <summary>Value of the Authorization header for requests to the demon</summary>
    public string GetAuthorizationHeader(Node node)
        => "Bearer " + NodeToken.Format(node.TokenId, _nodeService.GetToken(node));

    // ─── Errors ─────────────────────────────────────────────────────────────

    /// <summary>True for every error a demon call can throw besides cancellation by the caller</summary>
    public static bool IsNodeError(Exception exception, CancellationToken cancellationToken = default)
        => exception is HttpRequestException or NodeException or JsonException ||
           (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    /// <summary>
    /// Converts a demon error into a problem response: conflicts reported by the demon (e.g. "server is not running")
    /// keep their status code, everything else becomes 502.
    /// </summary>
    public static HttpApiException ToApiException(Exception exception) => exception switch
    {
        NodeException { StatusCode: StatusCodes.Status409Conflict } e => HttpApiException.Conflict(e.Message),
        NodeException e => new HttpApiException(StatusCodes.Status502BadGateway, "The node rejected the request", e.Message),
        _ => new HttpApiException(StatusCodes.Status502BadGateway, "The node is not reachable", Describe(exception))
    };

    /// <summary>
    /// Like <see cref="ToApiException"/>, but client errors of the demon (400 invalid path, 403 denylist, 404 file not found,
    /// 409, 413 too large, 415, 422) keep their status code so the browser sees why a file operation failed.
    /// </summary>
    public static HttpApiException ToProxyApiException(Exception exception) => exception switch
    {
        NodeException { StatusCode: 400 or 403 or 404 or 409 or 413 or 415 or 422 } e => new HttpApiException(e.StatusCode!.Value, e.Message),
        _ => ToApiException(exception)
    };

    private HttpRequestMessage CreateRequest(Node node, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(GetBaseUri(node), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NodeToken.Format(node.TokenId, _nodeService.GetToken(node)));
        request.Headers.Add(MidnightProtocol.HeaderName, MidnightProtocol.Version.ToString());
        return request;
    }

    private async Task<T> GetAsync<T>(Node node, string path, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var request = CreateRequest(node, HttpMethod.Get, path);
        using var response = await client.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, cancellationToken)
               ?? throw new NodeException("The demon sent an empty response");
    }

    private async Task SendAsync(Node node, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var request = CreateRequest(node, method, path);

        if (body != null)
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonDefaults.Options);

        using var response = await client.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>
    /// Sends the request without buffering the request or response body, the caller disposes the response.
    /// Only the connect timeout of the streaming client applies, the caller's token (request aborted) ends it.
    /// </summary>
    private async Task<HttpResponseMessage> SendStreamAsync(Node node, HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(StreamingHttpClientName);

        using var request = CreateRequest(node, method, path);
        request.Content = content;

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        try
        {
            await EnsureSuccessAsync(response, cancellationToken);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>Json request on the streaming client with an own timeout (file operations can take longer than 5 seconds)</summary>
    private async Task<T> SendLongAsync<T>(Node node, HttpMethod method, string path, object? body, TimeSpan timeout, CancellationToken cancellationToken, bool readBody = true)
    {
        var client = _httpClientFactory.CreateClient(StreamingHttpClientName);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        using var request = CreateRequest(node, method, path);

        if (body != null)
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonDefaults.Options);

        using var response = await client.SendAsync(request, timeoutSource.Token);

        await EnsureSuccessAsync(response, timeoutSource.Token);

        if (!readBody)
            return default!;

        return await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, timeoutSource.Token)
               ?? throw new NodeException("The demon sent an empty response");
    }

    /// <summary>Throws a NodeException with the problem title/detail of the demon if there is one</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = $"The demon responded with {(int)response.StatusCode} {response.ReasonPhrase}";

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<DemonProblem>(JsonDefaults.Options, cancellationToken);
            var text = string.Join(": ", new[] { problem?.Title, problem?.Detail }.Where(x => !string.IsNullOrWhiteSpace(x)));

            if (text.Length > 0)
                message = text;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Not a problem response, keep the status line
        }

        throw new NodeException(message, (int)response.StatusCode);
    }

    private sealed class DemonProblem
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
    }

    private static string Describe(Exception exception) => exception switch
    {
        TaskCanceledException => "Timeout while connecting to the demon",
        HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError } => "Connection refused or host unreachable",
        HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError } => "The hostname could not be resolved",
        HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError } => "SSL handshake failed",
        JsonException => "The demon sent an invalid response",
        _ => exception.Message
    };
}

/// <summary>The demon answered with an error. StatusCode is the http status of the demon response, if any.</summary>
public class NodeException(string message, int? statusCode = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}
