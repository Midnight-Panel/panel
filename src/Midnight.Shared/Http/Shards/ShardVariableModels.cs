namespace Midnight.Shared.Http.Shards;

/// <summary>Details of a shard variable</summary>
public class ShardVariableResponse
{
    public int Id { get; set; }
    public int ShardId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string EnvVariable { get; set; } = "";
    public string? DefaultValue { get; set; }
    public bool UserViewable { get; set; }
    public bool UserEditable { get; set; }
    public string Rules { get; set; } = "";
    public int SortOrder { get; set; }
}

/// <summary>Request model to create or update a shard variable</summary>
public class ShardVariableRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string EnvVariable { get; set; } = "";
    public string? DefaultValue { get; set; }
    public bool UserViewable { get; set; } = true;
    public bool UserEditable { get; set; } = true;
    public string Rules { get; set; } = "required|string";
    public int SortOrder { get; set; }
}

/// <summary>Request to validate user-provided values for shard variables</summary>
public class ValidateVariablesRequest
{
    public Dictionary<string, string?> Values { get; set; } = [];
}

/// <summary>Result of variable validation</summary>
public class ValidateVariablesResponse
{
    public bool IsValid => Errors.Count == 0;
    public Dictionary<string, string> Errors { get; set; } = [];
    public Dictionary<string, string> ResolvedEnvironment { get; set; } = [];
    public string? ResolvedStartupCommand { get; set; }
}
