using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>Who did something: a user (with ip) or the system (UserId null, e.g. schedules)</summary>
public record ActivityActor(int? UserId, string? IpAddress)
{
    public static readonly ActivityActor System = new(null, null);
}

/// <summary>
/// Activity log of servers. Properties are short strings (file names, commands), values are cut to 200 characters.
/// Never pass passwords or other secrets.
/// </summary>
public class ActivityService
{
    public const int MaxValueLength = 200;
    public const int MaxProperties = 16;

    private readonly DataContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(DataContext context, IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider, ILogger<ActivityService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>The user and ip of the current request (the ip respects the trusted forwarded headers)</summary>
    public ActivityActor GetCurrentActor()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext == null)
            return ActivityActor.System;

        var principal = httpContext.User;
        int? userId = principal.Identity?.IsAuthenticated == true && int.TryParse(principal.FindFirst(MidnightClaims.UserId)?.Value, out var id) ? id : null;

        return new ActivityActor(userId, httpContext.Connection.RemoteIpAddress?.ToString());
    }

    /// <summary>Writes an entry. Failures are logged and never break the action that was already done.</summary>
    /// <param name="actor">null = the user of the current request</param>
    public async Task LogAsync(int serverId, string eventName, IReadOnlyDictionary<string, string?>? properties = null, ActivityActor? actor = null)
    {
        actor ??= GetCurrentActor();

        var entry = new ServerActivity
        {
            ServerId = serverId,
            UserId = actor.UserId,
            IpAddress = actor.IpAddress,
            Event = eventName,
            PropertiesJson = JsonSerializer.Serialize(Sanitize(properties)),
            CreatedAt = _timeProvider.GetUtcNow()
        };

        try
        {
            // Own short lived entry: not affected by other pending changes of the request
            _context.ServerActivities.Add(entry);
            await _context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception e) when (e is DbUpdateException or InvalidOperationException)
        {
            _logger.LogWarning(e, "Could not write activity {Event} of server {ServerId}", eventName, serverId);
        }
        finally
        {
            _context.Entry(entry).State = EntityState.Detached;
        }
    }

    public async Task<PagedResult<ServerActivityResponse>> ListAsync(int serverId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = new PagedQuery { Page = page, PageSize = pageSize };
        var activities = _context.ServerActivities.AsNoTracking().Where(x => x.ServerId == serverId);

        var total = await activities.CountAsync(cancellationToken);

        var items = await activities
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(x => new { Activity = x, Username = x.User == null ? null : x.User.Username })
            .ToListAsync(cancellationToken);

        return new PagedResult<ServerActivityResponse>
        {
            Items = items.Select(x => new ServerActivityResponse
            {
                Id = x.Activity.Id,
                Event = x.Activity.Event,
                Properties = x.Activity.GetProperties(),
                UserId = x.Activity.UserId,
                Username = x.Username,
                IpAddress = x.Activity.IpAddress,
                CreatedAt = x.Activity.CreatedAt
            }).ToList(),
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            TotalCount = total
        };
    }

    public static Dictionary<string, string> Sanitize(IReadOnlyDictionary<string, string?>? properties)
    {
        var result = new Dictionary<string, string>();

        if (properties == null)
            return result;

        foreach (var (key, value) in properties.Take(MaxProperties))
        {
            if (value == null)
                continue;

            result[key] = Truncate(value);
        }

        return result;
    }

    public static string Truncate(string value)
        => value.Length <= MaxValueLength ? value : value[..(MaxValueLength - 1)] + "…";
}

/// <summary>Event names of the activity log</summary>
public static class ActivityEvents
{
    public const string PowerPrefix = "server:power.";
    public const string Command = "server:console.command";
    public const string Rename = "server:settings.rename";
    public const string Reinstall = "server:settings.reinstall";
    public const string StartupUpdate = "server:startup.update";

    public const string FileWrite = "server:file.write";
    public const string FileCreateFolder = "server:file.create-directory";
    public const string FileRename = "server:file.rename";
    public const string FileCopy = "server:file.copy";
    public const string FileDelete = "server:file.delete";
    public const string FileCompress = "server:file.compress";
    public const string FileDecompress = "server:file.decompress";
    public const string FileChmod = "server:file.chmod";
    public const string FilePull = "server:file.pull";
    public const string FileUpload = "server:file.upload";
    public const string FileDownload = "server:file.download";

    public const string BackupCreate = "server:backup.create";
    public const string BackupComplete = "server:backup.complete";
    public const string BackupFail = "server:backup.fail";
    public const string BackupDelete = "server:backup.delete";
    public const string BackupLock = "server:backup.lock";
    public const string BackupUnlock = "server:backup.unlock";
    public const string BackupDownload = "server:backup.download";
    public const string BackupRestore = "server:backup.restore";
    public const string BackupRestoreComplete = "server:backup.restore-complete";
    public const string BackupRestoreFail = "server:backup.restore-fail";

    public const string AllocationCreate = "server:allocation.create";
    public const string AllocationNotes = "server:allocation.notes";
    public const string AllocationPrimary = "server:allocation.primary";
    public const string AllocationDelete = "server:allocation.delete";

    public const string SubuserCreate = "server:subuser.create";
    public const string SubuserUpdate = "server:subuser.update";
    public const string SubuserDelete = "server:subuser.delete";

    public const string ScheduleCreate = "server:schedule.create";
    public const string ScheduleUpdate = "server:schedule.update";
    public const string ScheduleDelete = "server:schedule.delete";
    public const string ScheduleExecute = "server:schedule.execute";
    public const string ScheduleRun = "server:schedule.run";

    public const string DatabaseCreate = "server:database.create";
    public const string DatabaseRotatePassword = "server:database.rotate-password";
    public const string DatabaseDelete = "server:database.delete";

    public static string Power(Shared.Enums.PowerAction action) => PowerPrefix + action.ToString().ToLowerInvariant();
}
