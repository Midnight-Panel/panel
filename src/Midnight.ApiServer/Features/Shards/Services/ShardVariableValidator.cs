using System.Text.RegularExpressions;

namespace Midnight.ApiServer.Features.Shards.Services;

/// <summary>
/// Validation engine for shard variables.
/// Supports rules such as: required, string, numeric, integer, boolean, min:val, max:val, between:min,max, regex:pattern
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
                return (false, $"'{variableName}' is required.");
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
                    return (false, $"'{variableName}' must be a boolean (true/false or 1/0).");
                continue;
            }

            if (rule.Equals("numeric", StringComparison.OrdinalIgnoreCase))
            {
                if (!double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                    return (false, $"'{variableName}' must be a number.");
                continue;
            }

            if (rule.Equals("integer", StringComparison.OrdinalIgnoreCase))
            {
                if (!long.TryParse(value, out _))
                    return (false, $"'{variableName}' must be an integer.");
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
                            return (false, $"'{variableName}' must be at least {min}.");
                    }
                    else if (value.Length < min)
                    {
                        return (false, $"'{variableName}' must be at least {min} characters long.");
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
                            return (false, $"'{variableName}' may not exceed {max}.");
                    }
                    else if (value.Length > max)
                    {
                        return (false, $"'{variableName}' may not exceed {max} characters.");
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
                            return (false, $"'{variableName}' must be between {bMin} and {bMax}.");
                    }
                    else if (value.Length < bMin || value.Length > bMax)
                    {
                        return (false, $"'{variableName}' must be between {bMin} and {bMax} characters.");
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
                        return (false, $"'{variableName}' does not match the required pattern.");
                }
                catch (ArgumentException)
                {
                    // Skip if regex syntax in rule definition is invalid
                }
                continue;
            }
        }

        return (true, null);
    }
}
