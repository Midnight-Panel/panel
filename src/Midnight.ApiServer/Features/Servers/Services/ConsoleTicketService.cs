using Microsoft.Extensions.Caching.Memory;
using Midnight.ApiServer.Core.Security;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>
/// Browsers can't send the bearer token when opening a websocket. They get a short lived, single use ticket
/// over the normal api first and pass it as query parameter: /api/servers/{uuid}/console?ticket=...
/// </summary>
public class ConsoleTicketService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly SingleUseTicketStore<ConsoleTicket> _store;

    public ConsoleTicketService(IMemoryCache cache)
    {
        _store = new SingleUseTicketStore<ConsoleTicket>(cache, "console-ticket:");
    }

    /// <param name="permissions">Effective server permissions of the user when the ticket is issued</param>
    public string Create(int userId, Guid serverUuid, IReadOnlyList<string> permissions)
        => _store.Create(new ConsoleTicket(userId, serverUuid, permissions), Lifetime);

    /// <summary>Consumes the ticket. Null if it is unknown, expired, already used or for another server.</summary>
    public ConsoleTicket? Redeem(string? ticket, Guid serverUuid)
        => _store.Redeem(ticket, x => x.ServerUuid == serverUuid);
}

public record ConsoleTicket(int UserId, Guid ServerUuid, IReadOnlyList<string> Permissions);

/// <summary>
/// Plain links (downloads) can't carry the bearer token either: the browser gets a single use url that is
/// valid for 60 seconds and bound to the server and the file / backup.
/// </summary>
public class DownloadTicketService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly SingleUseTicketStore<DownloadTicket> _store;

    public DownloadTicketService(IMemoryCache cache)
    {
        _store = new SingleUseTicketStore<DownloadTicket>(cache, "download-ticket:");
    }

    public string CreateForFile(int userId, Guid serverUuid, string file)
        => _store.Create(new DownloadTicket(userId, serverUuid, DownloadKind.File, file), Lifetime);

    public string CreateForBackup(int userId, Guid serverUuid, Guid backupUuid)
        => _store.Create(new DownloadTicket(userId, serverUuid, DownloadKind.Backup, backupUuid.ToString()), Lifetime);

    /// <summary>Consumes the ticket. Null if it is unknown, expired, used or for another server / kind.</summary>
    public DownloadTicket? Redeem(string? ticket, Guid serverUuid, DownloadKind kind)
        => _store.Redeem(ticket, x => x.ServerUuid == serverUuid && x.Kind == kind);
}

public enum DownloadKind
{
    File,
    Backup
}

/// <param name="Target">File path or backup uuid</param>
public record DownloadTicket(int UserId, Guid ServerUuid, DownloadKind Kind, string Target);

/// <summary>Random tickets in the memory cache that can be redeemed exactly once</summary>
internal sealed class SingleUseTicketStore<T>(IMemoryCache cache, string prefix) where T : class
{
    public string Create(T value, TimeSpan lifetime)
    {
        var ticket = SecureTokens.GenerateAlphanumeric(48);
        cache.Set(prefix + ticket, new Entry(value), lifetime);
        return ticket;
    }

    /// <summary>The ticket is consumed even if the check fails (a ticket for another target is burned)</summary>
    public T? Redeem(string? ticket, Func<T, bool> isValid)
    {
        if (string.IsNullOrEmpty(ticket) || ticket.Length > 128)
            return null;

        var key = prefix + ticket;

        if (!cache.TryGetValue(key, out Entry? entry) || entry == null)
            return null;

        cache.Remove(key);

        // Two parallel requests with the same ticket: only one of them wins
        if (Interlocked.Exchange(ref entry.Used, 1) != 0)
            return null;

        return isValid(entry.Value) ? entry.Value : null;
    }

    private sealed class Entry(T value)
    {
        public T Value { get; } = value;
        public int Used;
    }
}
