using Midnight.ApiServer.Core.Http;
using Midnight.Shared.Http.Application;

namespace Midnight.ApiServer.Core;

/// <summary>A new external id for an update, Value null = remove it. Passing no change (null) keeps the current id.</summary>
public record ExternalIdChange(string? Value);

/// <summary>Ids of users and servers in external systems (billing). Unique per entity type when set.</summary>
public static class ExternalIds
{
    /// <summary>Trimmed, null when empty. 400 when too long.</summary>
    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
            return null;

        if (trimmed.Length > ApplicationApi.ExternalIdMaxLength)
        {
            throw new HttpApiException(StatusCodes.Status400BadRequest, "Invalid external id", $"The external id can have at most {ApplicationApi.ExternalIdMaxLength} characters")
            {
                Errors = new Dictionary<string, string> { ["ExternalId"] = $"At most {ApplicationApi.ExternalIdMaxLength} characters" }
            };
        }

        return trimmed;
    }

    public static HttpApiException InUse() => new(StatusCodes.Status409Conflict, "External id is already in use")
    {
        Errors = new Dictionary<string, string> { ["ExternalId"] = "Another entry already has this external id" }
    };
}
