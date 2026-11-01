using System.Net;
using System.Net.Sockets;

namespace Midnight.ApiServer.Core.Security;

/// <summary>Allow lists of single IPs and CIDR ranges ("203.0.113.7", "10.0.0.0/8", "2001:db8::/32")</summary>
public static class IpRules
{
    /// <summary>
    /// Parses an IPv4/IPv6 address or CIDR range into its canonical form. IPv4-mapped IPv6 addresses become IPv4,
    /// host bits of ranges are cleared ("10.1.2.3/8" => "10.0.0.0/8").
    /// </summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = "";
        var input = value?.Trim() ?? "";

        if (input.Length == 0)
            return false;

        var slash = input.IndexOf('/');

        if (slash < 0)
        {
            if (!IPAddress.TryParse(input, out var address) || input.Contains('%'))
                return false;

            normalized = Normalize(address).ToString();
            return true;
        }

        if (!IPAddress.TryParse(input[..slash], out var network) || input.Contains('%') ||
            !int.TryParse(input[(slash + 1)..], System.Globalization.NumberStyles.None, null, out var prefix))
            return false;

        var mapped = network.IsIPv4MappedToIPv6;
        network = Normalize(network);

        // "::ffff:10.0.0.0/104" is the same range as "10.0.0.0/8"
        if (mapped)
            prefix -= 96;

        var maxPrefix = network.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;

        if (prefix < 0 || prefix > maxPrefix)
            return false;

        normalized = new IPNetwork(Mask(network, prefix), prefix).ToString();
        return true;
    }

    /// <summary>True when the list is empty or the address matches one of the (normalized) entries</summary>
    public static bool IsAllowed(IPAddress? address, IReadOnlyCollection<string> rules)
    {
        if (rules.Count == 0)
            return true;

        if (address == null)
            return false;

        address = Normalize(address);

        foreach (var rule in rules)
        {
            if (rule.Contains('/'))
            {
                if (IPNetwork.TryParse(rule, out var network) && network.Contains(address))
                    return true;
            }
            else if (IPAddress.TryParse(rule, out var single) && Normalize(single).Equals(address))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Clients connecting over an IPv6 socket show up as ::ffff:a.b.c.d, rules use the IPv4 form</summary>
    public static IPAddress Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4();

        // Link local zones ("%eth0") are not part of a rule
        return address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0 ? new IPAddress(address.GetAddressBytes()) : address;
    }

    private static IPAddress Mask(IPAddress address, int prefix)
    {
        var bytes = address.GetAddressBytes();

        for (var i = 0; i < bytes.Length; i++)
        {
            var bits = Math.Clamp(prefix - i * 8, 0, 8);
            bytes[i] &= (byte)(0xFF << (8 - bits));
        }

        return new IPAddress(bytes);
    }
}
