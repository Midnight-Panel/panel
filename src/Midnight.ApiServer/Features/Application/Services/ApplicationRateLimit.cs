using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Midnight.ApiServer.Core.Security;

namespace Midnight.ApiServer.Features.Application.Services;

/// <summary>
/// Rate limit of the application api: a fixed window per application key (Api.ApplicationRateLimitPerMinute, default 240),
/// requests without a valid key share one window per ip. Needs UseAuthentication before UseRateLimiter.
/// </summary>
public static class ApplicationRateLimit
{
    public const string PolicyName = "application";
    public const int DefaultPermitLimit = 240;

    public static string GetPartitionKey(HttpContext context)
    {
        var keyId = context.User.GetApplicationKeyId();

        return keyId.HasValue
            ? $"key:{keyId.Value}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    /// <summary>Registers the policy used by the application controllers ([EnableRateLimiting(PolicyName)])</summary>
    public static RateLimiterOptions AddApplicationPolicy(this RateLimiterOptions options, int permitLimit)
    {
        options.AddPolicy(PolicyName, context => GetPartition(context, permitLimit));
        return options;
    }

    /// <summary>Adds the Retry-After header (seconds) to a 429 response when the limiter knows it</summary>
    public static void SetRetryAfter(OnRejectedContext context)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
    }

    public static RateLimitPartition<string> GetPartition(HttpContext context, int permitLimit)
        => RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, permitLimit),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
}
