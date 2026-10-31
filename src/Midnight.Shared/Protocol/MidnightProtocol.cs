namespace Midnight.Shared.Protocol;

/// <summary>
/// Versioning of the panel &lt;-&gt; daemon protocol. Both sides send the header on every request
/// and reject requests with an incompatible major version.
/// </summary>
public static class MidnightProtocol
{
    public const string HeaderName = "X-Midnight-Protocol";
    public const int Version = 1;
}
