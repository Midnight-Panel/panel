using Midnight.ApiServer.Features.Shards.Entities;

namespace Midnight.ApiServer.Features.Servers.Entities;

/// <summary>Value of a shard variable for one server. Shard variables without a row use their default value.</summary>
public class ServerVariable
{
    public int Id { get; set; }

    public int ServerId { get; set; }
    public Server Server { get; set; } = null!;

    public int ShardVariableId { get; set; }
    public ShardVariable ShardVariable { get; set; } = null!;

    public string Value { get; set; } = "";
}
