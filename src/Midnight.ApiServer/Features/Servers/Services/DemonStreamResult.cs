using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>Streams a demon response to the browser without buffering it</summary>
public static class DemonStreamResult
{
    /// <param name="downloadName">Set = Content-Disposition attachment with this file name</param>
    public static async Task<IActionResult> CreateAsync(HttpContext httpContext, HttpResponseMessage response, string? downloadName, string? contentType = null)
    {
        // Disposed after the body was written
        httpContext.Response.RegisterForDispose(response);

        var stream = await response.Content.ReadAsStreamAsync(httpContext.RequestAborted);
        var type = contentType ?? response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

        if (response.Content.Headers.ContentLength is { } length)
            httpContext.Response.ContentLength = length;

        // Browsers must never render proxied files inline as html
        httpContext.Response.Headers[HeaderNames.CacheControl] = "no-store";

        return new FileStreamResult(stream, type)
        {
            FileDownloadName = downloadName,
            EnableRangeProcessing = false
        };
    }

    /// <summary>"a.txt" of "/folder/a.txt", "download" if there is no name</summary>
    public static string GetFileName(string path)
    {
        var name = Path.GetFileName(path.Replace('\\', '/').TrimEnd('/'));
        return string.IsNullOrWhiteSpace(name) ? "download" : name;
    }
}
