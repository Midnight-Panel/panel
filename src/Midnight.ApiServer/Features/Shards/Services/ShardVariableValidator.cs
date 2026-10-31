using System.Text.RegularExpressions;

namespace Midnight.ApiServer.Features.Shards.Services;

/// <summary>
/// Validierungs-Engine für Shard-Variablen.
/// Unterstützt Regeln wie: required, string, numeric, integer, boolean, min:val, max:val, between:min,max, regex:pattern
/// </summary>
public static class ShardVariableValidator
{
    public static (bool IsValid, string? Error) ValidateValue(string? rawValue, string rules, string variableName)
    {
        var value = rawValue?.Trim();
        var ruleList = rules.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var isRequired = ruleList.Any(r => r.Equals("required", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(value))
        {
            if (isRequired)
                return (false, $"'{variableName}' ist ein Pflichtfeld.");
            return (true, null);
        }

        foreach (var rule in ruleList)
        {
            if (rule.Equals("required", StringComparison.OrdinalIgnoreCase))
                continue;

            if (rule.Equals("string", StringComparison.OrdinalIgnoreCase))
                continue;

            if (rule.Equals("boolean", StringComparison.OrdinalIgnoreCase))
            {
                if (!bool.TryParse(value, out _) && value != "0" && value != "1")
                    return (false, $"'{variableName}' muss ein Wahrheitswert (true/false oder 1/0) sein.");
                continue;
            }

            if (rule.Equals("numeric", StringComparison.OrdinalIgnoreCase))
            {
                if (!double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                    return (false, $"'{variableName}' muss eine Zahl sein.");
                continue;
            }

            if (rule.Equals("integer", StringComparison.OrdinalIgnoreCase))
            {
                if (!long.TryParse(value, out _))
                    return (false, $"'{variableName}' muss eine Ganzzahl sein.");
                continue;
            }

            if (rule.StartsWith("min:", StringComparison.OrdinalIgnoreCase))
            {
                var minStr = rule[4..];
                if (double.TryParse(minStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var min))
                {
                    if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numVal))
                    {
                        if (numVal < min)
                            return (false, $"'{variableName}' muss mindestens {min} sein.");
                    }
                    else if (value.Length < min)
                    {
                        return (false, $"'{variableName}' muss mindestens {min} Zeichen lang sein.");
                    }
                }
                continue;
            }

            if (rule.StartsWith("max:", StringComparison.OrdinalIgnoreCase))
            {
                var maxStr = rule[4..];
                if (double.TryParse(maxStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var max))
                {
                    if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numVal))
                    {
                        if (numVal > max)
                            return (false, $"'{variableName}' darf maximal {max} sein.");
                    }
                    else if (value.Length > max)
                    {
                        return (false, $"'{variableName}' darf maximal {max} Zeichen lang sein.");
                    }
                }
                continue;
            }

            if (rule.StartsWith("between:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = rule[8..].Split(',');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var bMin) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var bMax))
                {
                    if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numVal))
                    {
                        if (numVal < bMin || numVal > bMax)
                            return (false, $"'{variableName}' muss zwischen {bMin} und {bMax} liegen.");
                    }
                    else if (value.Length < bMin || value.Length > bMax)
                    {
                        return (false, $"'{variableName}' muss zwischen {bMin} und {bMax} Zeichen lang sein.");
                    }
                }
                continue;
            }

            if (rule.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
            {
                var pattern = rule[6..];
                try
                {
                    if (!Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200)))
                        return (false, $"'{variableName}' entspricht nicht dem geforderten Format.");
                }
                catch (ArgumentException)
                {
                    // Bei ungültigem Regex im Regelstring überspringen
                }
                continue;
            }
        }

        return (true, null);
    }
}
