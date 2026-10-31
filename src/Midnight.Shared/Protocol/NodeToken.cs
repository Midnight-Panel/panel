namespace Midnight.Shared.Protocol;

/// <summary>
/// Node tokens are sent as "Authorization: Bearer mdn_{tokenId}.{token}" in both directions
/// (panel to daemon and daemon to panel).
/// </summary>
public static class NodeToken
{
    public const string Prefix = "mdn_";

    public static string Format(string tokenId, string token) => $"{Prefix}{tokenId}.{token}";

    public static bool TryParse(string? value, out string tokenId, out string token)
    {
        tokenId = token = "";

        if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var separator = value.IndexOf('.', Prefix.Length);

        if (separator <= Prefix.Length || separator == value.Length - 1)
            return false;

        tokenId = value[Prefix.Length..separator];
        token = value[(separator + 1)..];

        return true;
    }
}
