using System.Text.Json;
using System.Text.Json.Serialization;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.Shared.Http.Application;

namespace Midnight.ApiServer.Features.Application.Entities;

/// <summary>
/// Application api key "mda_{identifier}_{secret}" for automation (billing systems). Not bound to a user:
/// its access is defined per resource. The identifier is stored in plain text for lookup, the token only as SHA-256 hash.
/// </summary>
public class ApplicationKey
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    public int Id { get; set; }

    /// <summary>Random [a-z0-9] part between "mda_" and the secret</summary>
    public string Identifier { get; set; } = "";

    public string KeyHash { get; set; } = "";

    public string Description { get; set; } = "";

    /// <summary>Json object resource name => access name, e.g. {"Servers":"ReadWrite"}. Missing resources = None.</summary>
    public string PermissionsJson { get; set; } = "{}";

    /// <summary>Normalized IPs and CIDR ranges, empty = every ip</summary>
    public List<string> AllowedIps { get; set; } = [];

    /// <summary>Actions of the key are attributed to this user. Null once the user was deleted (the key keeps working).</summary>
    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Every resource with its access (None for missing or unknown entries)</summary>
    public Dictionary<ApplicationResource, ApplicationAccess> GetPermissions()
    {
        Dictionary<string, string>? stored;

        try
        {
            stored = JsonSerializer.Deserialize<Dictionary<string, string>>(PermissionsJson);
        }
        catch (JsonException)
        {
            stored = null;
        }

        var result = new Dictionary<ApplicationResource, ApplicationAccess>();

        foreach (var resource in Enum.GetValues<ApplicationResource>())
        {
            var access = ApplicationAccess.None;

            if (stored != null && stored.TryGetValue(resource.ToString(), out var value) && Enum.TryParse<ApplicationAccess>(value, out var parsed) && Enum.IsDefined(parsed))
                access = parsed;

            result[resource] = access;
        }

        return result;
    }

    public void SetPermissions(IReadOnlyDictionary<ApplicationResource, ApplicationAccess> permissions)
    {
        var stored = permissions
            .Where(x => x.Value != ApplicationAccess.None)
            .OrderBy(x => x.Key)
            .ToDictionary(x => x.Key.ToString(), x => x.Value.ToString());

        PermissionsJson = JsonSerializer.Serialize(stored, JsonOptions);
    }
}
