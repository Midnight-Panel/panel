using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Daemon;
using Midnight.Shared.Http.Nodes;
using Midnight.Shared.Protocol;

namespace Midnight.ApiServer.Features.Nodes.Services;

/// <summary>Calls the daemon api of a node with its node token</summary>
public class NodeClient
{
    public const string HttpClientName = "midnight-daemon";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NodeService _nodeService;
    private readonly ILogger<NodeClient> _logger;

    public NodeClient(IHttpClientFactory httpClientFactory, NodeService nodeService, ILogger<NodeClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _nodeService = nodeService;
        _logger = logger;
    }

    public static Uri GetBaseUri(Node node)
        => new($"{(node.UseSsl ? "https" : "http")}://{node.Fqdn}:{node.HttpPort}/");

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

    public Task<DaemonLogsResponse> GetLogsAsync(Node node, int lines, CancellationToken cancellationToken)
        => GetAsync<DaemonLogsResponse>(node, $"api/system/logs?lines={lines}", cancellationToken);

    private async Task<T> GetAsync<T>(Node node, string path, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(GetBaseUri(node), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NodeToken.Format(node.TokenId, _nodeService.GetToken(node)));
        request.Headers.Add(MidnightProtocol.HeaderName, MidnightProtocol.Version.ToString());

        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new NodeException($"The daemon responded with {(int)response.StatusCode} {response.ReasonPhrase}");

        return await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, cancellationToken)
               ?? throw new NodeException("The daemon sent an empty response");
    }

    private static string Describe(Exception exception) => exception switch
    {
        TaskCanceledException => "Timeout while connecting to the daemon",
        HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError } => "Connection refused or host unreachable",
        HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError } => "The hostname could not be resolved",
        HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError } => "SSL handshake failed",
        _ => exception.Message
    };
}

public class NodeException(string message) : Exception(message);
