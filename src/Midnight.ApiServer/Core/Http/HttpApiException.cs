namespace Midnight.ApiServer.Core.Http;

/// <summary>
/// Thrown by services for expected errors. Converted into a ProblemDetails response
/// with the given status code by <see cref="ApiExceptionHandler"/>.
/// </summary>
public class HttpApiException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }

    public HttpApiException(int statusCode, string title, string? detail = null) : base(detail ?? title)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public static HttpApiException BadRequest(string title, string? detail = null) => new(StatusCodes.Status400BadRequest, title, detail);
    public static HttpApiException Unauthorized(string title, string? detail = null) => new(StatusCodes.Status401Unauthorized, title, detail);
    public static HttpApiException Forbidden(string title, string? detail = null) => new(StatusCodes.Status403Forbidden, title, detail);
    public static HttpApiException NotFound(string title, string? detail = null) => new(StatusCodes.Status404NotFound, title, detail);
    public static HttpApiException Conflict(string title, string? detail = null) => new(StatusCodes.Status409Conflict, title, detail);
}
