using System.Net;
using System.Net.Sockets;

namespace Midnight.ApiServer.Core.Security;

/// <summary>Thrown when an outbound download is rejected or fails. The message is safe to show to admins.</summary>
public class SafeDownloadException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>A downloaded or copied payload in a temporary file that is deleted when the stream is disposed.</summary>
public sealed record SafeDownloadResult(FileStream Content, Uri FinalUri) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>
/// Downloads files from user supplied URLs with SSRF protection: only http/https, the target
/// address is resolved and checked inside the connect callback (so the checked IP is the one
/// connected to), redirects are followed manually and re-validated, and the body is streamed
/// to a temporary file with a hard size limit and timeout.
/// </summary>
public static class SafeDownloader
{
    public const int MaxRedirects = 5;
    private const int BufferSize = 81920;

    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(15),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = ConnectToPublicAddressAsync
    })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public static bool TryParseHttpUrl(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed))
            return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            return false;
        uri = parsed;
        return true;
    }

    public static async Task<SafeDownloadResult> DownloadToTempFileAsync(
        string url, long maxBytes, TimeSpan timeout, string userAgent, CancellationToken cancellationToken)
    {
        if (!TryParseHttpUrl(url, out var uri))
            throw new SafeDownloadException("Please provide a valid http:// or https:// URL.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            for (var redirects = 0; ; redirects++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd(userAgent);

                using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                if (IsRedirect(response.StatusCode))
                {
                    if (redirects >= MaxRedirects)
                        throw new SafeDownloadException("Too many redirects.");
                    var location = response.Headers.Location
                        ?? throw new SafeDownloadException("Redirect response without a location.");
                    var next = location.IsAbsoluteUri ? location : new Uri(uri, location);
                    if (next.Scheme != Uri.UriSchemeHttp && next.Scheme != Uri.UriSchemeHttps)
                        throw new SafeDownloadException("Redirects to non-http(s) URLs are not allowed.");
                    uri = next;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    throw new SafeDownloadException($"The server responded with {(int)response.StatusCode} {response.ReasonPhrase}.");

                if (response.Content.Headers.ContentLength > maxBytes)
                    throw new SafeDownloadException($"The file exceeds the maximum size of {maxBytes / (1024 * 1024)} MB.");

                await using var body = await response.Content.ReadAsStreamAsync(cts.Token);
                var file = await CopyToTempFileAsync(body, maxBytes, cts.Token);
                return new SafeDownloadResult(file, uri);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SafeDownloadException("The download timed out.");
        }
        catch (HttpRequestException ex)
        {
            var blocked = ex.InnerException as SafeDownloadException;
            throw blocked ?? new SafeDownloadException($"Download failed: {ex.Message}", ex);
        }
    }

    /// <summary>Copies a stream into a self-deleting temporary file, aborting once more than <paramref name="maxBytes"/> are read.</summary>
    public static async Task<FileStream> CopyToTempFileAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        var file = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None,
            BufferSize, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            var buffer = new byte[BufferSize];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > maxBytes)
                    throw new SafeDownloadException($"The file exceeds the maximum size of {maxBytes / (1024 * 1024)} MB.");
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            file.Position = 0;
            return file;
        }
        catch
        {
            await file.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Returns false for loopback, private, link-local (incl. cloud metadata), CGNAT, multicast,
    /// unspecified, documentation/benchmark and other reserved ranges.
    /// </summary>
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var b = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(b[0] == 0                                    // 0.0.0.0/8
                || b[0] == 10                                     // 10.0.0.0/8
                || b[0] == 127                                    // loopback
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)     // CGNAT 100.64.0.0/10
                || (b[0] == 169 && b[1] == 254)                   // link-local / metadata
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)      // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)        // 192.0.0.0/24
                || (b[0] == 192 && b[1] == 0 && b[2] == 2)        // TEST-NET-1
                || (b[0] == 192 && b[1] == 168)                   // 192.168.0.0/16
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))    // benchmarking
                || (b[0] == 198 && b[1] == 51 && b[2] == 100)     // TEST-NET-2
                || (b[0] == 203 && b[1] == 0 && b[2] == 113)      // TEST-NET-3
                || b[0] >= 224);                                  // multicast, reserved, broadcast
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast ||
                address.IsIPv6UniqueLocal || address.IsIPv6Teredo)
                return false;
            if (b.AsSpan(0, 12).IndexOfAnyExcept((byte)0) < 0)    // ::, ::1 and IPv4-compatible
                return false;
            if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b) // NAT64 64:ff9b::/96
                return IsPublicAddress(new IPAddress(b.AsSpan(12, 4)));
            if (b[0] == 0x20 && b[1] == 0x02)                     // 6to4 2002::/16
                return IsPublicAddress(new IPAddress(b.AsSpan(2, 4)));
            if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) // documentation
                return false;
            return true;
        }

        return false;
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0)
            throw new SafeDownloadException($"Could not resolve host '{host}'.");
        if (addresses.Any(a => !IsPublicAddress(a)))
            throw new SafeDownloadException("Downloads from local or private network addresses are not allowed.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static bool IsRedirect(HttpStatusCode code) => code is
        HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
}
