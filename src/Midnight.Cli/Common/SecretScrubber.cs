using System.Text.RegularExpressions;

namespace Midnight.Cli.Common;

/// <summary>
/// Makes sure passwords, hashes, API tokens and secrets
/// never end up in console output, process logs or error messages.
/// </summary>
public static partial class SecretScrubber
{
    private static readonly string[] SecretKeywords =
    [
        "POSTGRES_PASSWORD",
        "MIDNIGHT_ADMIN_PASSWORD",
        "PASSWORD",
        "SECRET",
        "TOKEN",
        "JWT",
        "KEY",
        "PASSPHRASE"
    ];

    [GeneratedRegex(@"(?i)(password|secret|token|key|passphrase)\s*[:=]\s*([^\s,;]+)", RegexOptions.Compiled)]
    private static partial Regex KeyValuePattern();

    [GeneratedRegex(@"(?i)(--password|--secret|--token|--key|--passphrase)\s+([^\s]+)", RegexOptions.Compiled)]
    private static partial Regex CliFlagPattern();

    [GeneratedRegex(@"(?i)(Password)=([^;]+)", RegexOptions.Compiled)]
    private static partial Regex ConnectionStringPasswordPattern();

    /// <summary>
    /// Entfernt sensible Daten aus einem String.
    /// </summary>
    public static string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var result = input;

        // Key-Value-Zuweisungen (z.B. in .env oder JSON)
        result = KeyValuePattern().Replace(result, m =>
        {
            var key = m.Groups[1].Value;
            return $"{key}=********";
        });

        // CLI-Flags (z.B. --password geheim)
        result = CliFlagPattern().Replace(result, m =>
        {
            var flag = m.Groups[1].Value;
            return $"{flag} ********";
        });

        // Connection-Strings (z.B. Password=abc;)
        result = ConnectionStringPasswordPattern().Replace(result, "$1=********");

        return result;
    }
}
