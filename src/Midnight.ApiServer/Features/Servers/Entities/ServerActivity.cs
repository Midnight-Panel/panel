using System.Text.Json;
using Midnight.ApiServer.Features.Users.Entities;

namespace Midnight.ApiServer.Features.Servers.Entities;

/// <summary>One entry of the activity log of a server</summary>
public class ServerActivity
{
    public long Id { get; set; }

    public int ServerId { get; set; }
    public Server Server { get; set; } = null!;

    /// <summary>The acting user, null for the system (schedules) or deleted users</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>e.g. "server:power.start"</summary>
    public string Event { get; set; } = "";

    /// <summary>Json object of short strings</summary>
    public string PropertiesJson { get; set; } = "{}";

    public string? IpAddress { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Dictionary<string, string> GetProperties()
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(PropertiesJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
