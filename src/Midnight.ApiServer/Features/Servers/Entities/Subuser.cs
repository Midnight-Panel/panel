using System.Text.Json;
using Midnight.ApiServer.Features.Users.Entities;

namespace Midnight.ApiServer.Features.Servers.Entities;

/// <summary>Another user with access to a server, limited to the listed ServerPermissions</summary>
public class Subuser
{
    public int Id { get; set; }

    public int ServerId { get; set; }
    public Server Server { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Json array of ServerPermissions strings</summary>
    public string PermissionsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<string> GetPermissions()
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(PermissionsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void SetPermissions(IEnumerable<string> permissions)
        => PermissionsJson = JsonSerializer.Serialize(permissions.Distinct().Order(StringComparer.Ordinal).ToList());
}
