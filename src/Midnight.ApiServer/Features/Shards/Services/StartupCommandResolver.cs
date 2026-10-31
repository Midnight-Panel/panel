using System.Text.RegularExpressions;

namespace Midnight.ApiServer.Features.Shards.Services;

/// <summary>
/// Extrahiert, validiert und ersetzt Platzhalter im Startbefehl eines Shards (z. B. {{SERVER_MEMORY}}, {{SERVER_JARFILE}}).
/// </summary>
public static partial class StartupCommandResolver
{
    /// <summary>Systemvariablen, die vom Daemon oder Panel automatisch bereitgestellt werden</summary>
    public static readonly HashSet<string> SystemVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SERVER_MEMORY",
        "SERVER_PORT",
        "SERVER_IP",
        "STARTUP"
    };

    [GeneratedRegex(@"\{\{([a-zA-Z0-9_-]+)\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    /// <summary>Extrahiert alle im Befehl vorkommenden {{PLATZHALTER}}</summary>
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
    /// Prüft, ob alle im Befehl verwendeten Platzhalter entweder Systemvariablen oder definierte Shard-Variablen sind.
    /// Gibt eine Liste von Fehlern zurück (leer wenn gültig).
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
                errors.Add($"Unbekannter Platzhalter '{{{{{placeholder}}}}}' im Startbefehl. Definiere die Variable zuerst.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Ersetzt alle Platzhalter durch die entsprechenden Werte.
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

            return match.Value; // Unverändert lassen, falls unbekannt
        });
    }
}
