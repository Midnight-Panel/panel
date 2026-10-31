using System.Text.RegularExpressions;

namespace Midnight.ApiServer.Features.Shards.Services;

/// <summary>
/// Extracts, validates, and resolves placeholders in a shard's startup command (e.g. {{SERVER_MEMORY}}, {{SERVER_JARFILE}}).
/// </summary>
public static partial class StartupCommandResolver
{
    /// <summary>System variables automatically provided by the daemon or panel</summary>
    public static readonly HashSet<string> SystemVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SERVER_MEMORY",
        "SERVER_PORT",
        "SERVER_IP",
        "STARTUP"
    };

    [GeneratedRegex(@"\{\{([a-zA-Z0-9_-]+)\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    /// <summary>Extracts all {{PLACEHOLDER}} tokens present in the startup command</summary>
    public static HashSet<string> ExtractPlaceholders(string startupCommand)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(startupCommand))
            return result;

        var matches = PlaceholderRegex().Matches(startupCommand);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
                result.Add(match.Groups[1].Value);
        }

        return result;
    }

    /// <summary>
    /// Checks whether all placeholders used in the startup command are either system variables or defined shard variables.
    /// Returns a list of validation errors (empty if valid).
    /// </summary>
    public static List<string> ValidatePlaceholders(string startupCommand, IEnumerable<string> definedVariables)
    {
        var errors = new List<string>();
        var allowed = new HashSet<string>(definedVariables, StringComparer.OrdinalIgnoreCase);
        allowed.UnionWith(SystemVariables);

        var usedPlaceholders = ExtractPlaceholders(startupCommand);
        foreach (var placeholder in usedPlaceholders)
        {
            if (!allowed.Contains(placeholder))
            {
                errors.Add($"Unknown placeholder '{{{{{placeholder}}}}}' in startup command. Define the variable first.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Replaces all placeholders with their corresponding values.
    /// </summary>
    public static string ResolveStartupCommand(
        string startupCommand,
        IDictionary<string, string> environment,
        int memoryMb = 1024,
        int port = 25565,
        string ip = "0.0.0.0")
    {
        if (string.IsNullOrWhiteSpace(startupCommand))
            return "";

        var env = new Dictionary<string, string>(environment, StringComparer.OrdinalIgnoreCase)
        {
            ["SERVER_MEMORY"] = memoryMb.ToString(),
            ["SERVER_PORT"] = port.ToString(),
            ["SERVER_IP"] = ip
        };

        return PlaceholderRegex().Replace(startupCommand, match =>
        {
            var key = match.Groups[1].Value;
            if (env.TryGetValue(key, out var val))
                return val ?? "";

            return match.Value; // Leave unchanged if unknown
        });
    }
}
