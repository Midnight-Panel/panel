using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.Shared.Enums;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>Schedules with 5 part cron expressions (Cronos) in the panel time zone and up to 10 tasks each</summary>
public class ScheduleService
{
    public const int MaxTasks = 10;

    private readonly DataContext _context;
    private readonly IOptionsMonitor<AppConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;

    public ScheduleService(DataContext context, IOptionsMonitor<AppConfiguration> configuration, TimeProvider timeProvider)
    {
        _context = context;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    private TimeZoneInfo TimeZone => PanelTimeZone.Get(_configuration.CurrentValue.TimeZone);

    // ─── Cron ───────────────────────────────────────────────────────────────

    /// <summary>Validates every part on its own (errors per field, e.g. "Hour") and the whole expression</summary>
    public static CronExpression ParseCron(string minute, string hour, string dayOfMonth, string month, string dayOfWeek)
    {
        var parts = new (string Field, string Value)[]
        {
            ("Minute", minute), ("Hour", hour), ("DayOfMonth", dayOfMonth), ("Month", month), ("DayOfWeek", dayOfWeek)
        };

        var errors = new Dictionary<string, string>();

        for (var i = 0; i < parts.Length; i++)
        {
            var value = parts[i].Value?.Trim() ?? "";

            if (value.Length == 0 || value.Any(char.IsWhiteSpace))
            {
                errors[parts[i].Field] = "Invalid value";
                continue;
            }

            // Only this part set, the others "*"
            var single = string.Join(' ', parts.Select((_, index) => index == i ? value : "*"));

            if (!TryParse(single, out _))
                errors[parts[i].Field] = $"'{value}' is not a valid cron value";
        }

        CronExpression? expression = null;

        if (errors.Count == 0 && !TryParse(string.Join(' ', parts.Select(x => x.Value.Trim())), out expression))
            errors["Minute"] = "The cron expression is invalid";

        if (errors.Count > 0 || expression == null)
            throw new HttpApiException(StatusCodes.Status400BadRequest, "Invalid cron expression", string.Join(" ", errors.Values)) { Errors = errors };

        return expression;
    }

    public static CronExpression ParseCron(Schedule schedule)
        => ParseCron(schedule.Minute, schedule.Hour, schedule.DayOfMonth, schedule.Month, schedule.DayOfWeek);

    /// <summary>Next occurrence strictly after <paramref name="from"/> in the given time zone (DST aware)</summary>
    public static DateTimeOffset? GetNextRun(CronExpression expression, DateTimeOffset from, TimeZoneInfo timeZone)
        => expression.GetNextOccurrence(from, timeZone);

    public DateTimeOffset? GetNextRun(Schedule schedule, DateTimeOffset from)
        => schedule.IsActive ? GetNextRun(ParseCron(schedule), from, TimeZone) : null;

    private static bool TryParse(string expression, out CronExpression? result)
    {
        try
        {
            result = CronExpression.Parse(expression, CronFormat.Standard);
            return true;
        }
        catch (CronFormatException)
        {
            result = null;
            return false;
        }
    }

    // ─── Schedules ──────────────────────────────────────────────────────────

    public async Task<List<Schedule>> ListAsync(Server server, CancellationToken cancellationToken)
        => await _context.Schedules
            .AsNoTracking()
            .Include(x => x.Tasks)
            .Where(x => x.ServerId == server.Id)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<Schedule> GetAsync(Server server, int id, CancellationToken cancellationToken)
        => await _context.Schedules.Include(x => x.Tasks).FirstOrDefaultAsync(x => x.Id == id && x.ServerId == server.Id, cancellationToken)
           ?? throw HttpApiException.NotFound("Schedule not found");

    public async Task<Schedule> CreateAsync(Server server, ScheduleRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var schedule = new Schedule { ServerId = server.Id, CreatedAt = now };

        Apply(schedule, request, now);

        _context.Schedules.Add(schedule);
        await _context.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    public async Task<Schedule> UpdateAsync(Server server, int id, ScheduleRequest request, CancellationToken cancellationToken)
    {
        var schedule = await GetAsync(server, id, cancellationToken);

        Apply(schedule, request, _timeProvider.GetUtcNow());
        await _context.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    public async Task<Schedule> DeleteAsync(Server server, int id, CancellationToken cancellationToken)
    {
        var schedule = await GetAsync(server, id, cancellationToken);

        _context.Schedules.Remove(schedule);
        await _context.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    private void Apply(Schedule schedule, ScheduleRequest request, DateTimeOffset now)
    {
        var expression = ParseCron(request.Minute, request.Hour, request.DayOfMonth, request.Month, request.DayOfWeek);

        schedule.Name = request.Name.Trim();
        schedule.Minute = request.Minute.Trim();
        schedule.Hour = request.Hour.Trim();
        schedule.DayOfMonth = request.DayOfMonth.Trim();
        schedule.Month = request.Month.Trim();
        schedule.DayOfWeek = request.DayOfWeek.Trim();
        schedule.IsActive = request.IsActive;
        schedule.OnlyWhenOnline = request.OnlyWhenOnline;
        schedule.NextRunAt = request.IsActive ? GetNextRun(expression, now, TimeZone) : null;
        schedule.UpdatedAt = now;
    }

    // ─── Tasks ──────────────────────────────────────────────────────────────

    /// <summary>409 with 10 tasks, the new task is appended (Sequence = last + 1)</summary>
    public async Task<ScheduleTask> AddTaskAsync(Server server, int scheduleId, ScheduleTaskRequest request, CancellationToken cancellationToken)
    {
        var schedule = await GetAsync(server, scheduleId, cancellationToken);

        if (schedule.Tasks.Count >= MaxTasks)
            throw HttpApiException.Conflict("Task limit reached", $"A schedule can have at most {MaxTasks} tasks");

        var task = new ScheduleTask
        {
            Sequence = schedule.Tasks.Count == 0 ? 1 : schedule.Tasks.Max(x => x.Sequence) + 1
        };

        ApplyTask(task, request);
        schedule.Tasks.Add(task);
        schedule.UpdatedAt = _timeProvider.GetUtcNow();

        await _context.SaveChangesAsync(cancellationToken);

        return task;
    }

    public async Task<ScheduleTask> UpdateTaskAsync(Server server, int scheduleId, int taskId, ScheduleTaskRequest request, CancellationToken cancellationToken)
    {
        var schedule = await GetAsync(server, scheduleId, cancellationToken);
        var task = schedule.Tasks.FirstOrDefault(x => x.Id == taskId) ?? throw HttpApiException.NotFound("Task not found");

        ApplyTask(task, request);
        schedule.UpdatedAt = _timeProvider.GetUtcNow();

        await _context.SaveChangesAsync(cancellationToken);

        return task;
    }

    /// <summary>The remaining tasks are re-numbered 1..n in their order</summary>
    public async Task<ScheduleTask> DeleteTaskAsync(Server server, int scheduleId, int taskId, CancellationToken cancellationToken)
    {
        var schedule = await GetAsync(server, scheduleId, cancellationToken);
        var task = schedule.Tasks.FirstOrDefault(x => x.Id == taskId) ?? throw HttpApiException.NotFound("Task not found");

        schedule.Tasks.Remove(task);
        _context.ScheduleTasks.Remove(task);

        var sequence = 1;

        foreach (var remaining in schedule.Tasks.OrderBy(x => x.Sequence).ThenBy(x => x.Id))
            remaining.Sequence = sequence++;

        schedule.UpdatedAt = _timeProvider.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);

        return task;
    }

    /// <summary>Command: not empty. Power: Start/Stop/Restart/Kill (stored as name). Backup: optional ignored files.</summary>
    public static string NormalizePayload(ScheduleTaskAction action, string? payload)
    {
        var value = payload?.Trim() ?? "";

        switch (action)
        {
            case ScheduleTaskAction.Command:
                if (value.Length == 0)
                    throw InvalidPayload("The command is required");

                return value;

            case ScheduleTaskAction.Power:
                if (!TryParsePower(value, out var power))
                    throw InvalidPayload("Use one of Start, Stop, Restart or Kill");

                return power.ToString();

            case ScheduleTaskAction.Backup:
                return BackupService.NormalizeIgnoredFiles(payload);

            default:
                throw InvalidPayload("Unknown action");
        }
    }

    public static bool TryParsePower(string? value, out PowerAction action)
        => Enum.TryParse(value, ignoreCase: true, out action) && Enum.IsDefined(action) && !int.TryParse(value, out _);

    private static void ApplyTask(ScheduleTask task, ScheduleTaskRequest request)
    {
        if (!Enum.IsDefined(request.Action))
            throw InvalidPayload("Unknown action");

        task.Action = request.Action;
        task.Payload = NormalizePayload(request.Action, request.Payload);
        task.TimeOffset = Math.Clamp(request.TimeOffset, 0, 900);
        task.ContinueOnFailure = request.ContinueOnFailure;
    }

    private static HttpApiException InvalidPayload(string message)
        => new(StatusCodes.Status400BadRequest, "Invalid task", message) { Errors = new Dictionary<string, string> { ["Payload"] = message } };

    // ─── Responses ──────────────────────────────────────────────────────────

    public static ScheduleResponse ToResponse(Schedule schedule) => new()
    {
        Id = schedule.Id,
        Name = schedule.Name,
        Minute = schedule.Minute,
        Hour = schedule.Hour,
        DayOfMonth = schedule.DayOfMonth,
        Month = schedule.Month,
        DayOfWeek = schedule.DayOfWeek,
        IsActive = schedule.IsActive,
        OnlyWhenOnline = schedule.OnlyWhenOnline,
        IsProcessing = schedule.IsProcessing,
        LastRunAt = schedule.LastRunAt,
        NextRunAt = schedule.NextRunAt,
        Tasks = schedule.Tasks.OrderBy(x => x.Sequence).ThenBy(x => x.Id).Select(ToResponse).ToList()
    };

    public static ScheduleTaskResponse ToResponse(ScheduleTask task) => new()
    {
        Id = task.Id,
        Sequence = task.Sequence,
        Action = task.Action,
        Payload = task.Payload,
        TimeOffset = task.TimeOffset,
        ContinueOnFailure = task.ContinueOnFailure
    };
}
