using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.Shared.Enums;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>Result of one schedule run</summary>
public enum ScheduleRunResult
{
    Completed,
    Failed,
    Skipped
}

/// <summary>Runs the tasks of one schedule in order (scoped, one instance per run)</summary>
public class ScheduleRunner
{
    private readonly DataContext _context;
    private readonly ServerService _serverService;
    private readonly BackupService _backupService;
    private readonly ActivityService _activity;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScheduleRunner> _logger;

    public ScheduleRunner(DataContext context, ServerService serverService, BackupService backupService, ActivityService activity, TimeProvider timeProvider,
        ILogger<ScheduleRunner> logger)
    {
        _context = context;
        _serverService = serverService;
        _backupService = backupService;
        _activity = activity;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// The schedule must already be claimed (IsProcessing = true); it is released at the end in every case.
    /// Waits TimeOffset seconds before each task, stops at the first failure unless the task has ContinueOnFailure.
    /// </summary>
    public async Task<ScheduleRunResult> RunAsync(int scheduleId, bool manual, ActivityActor actor, CancellationToken stoppingToken)
    {
        var startedAt = _timeProvider.GetUtcNow();
        var result = ScheduleRunResult.Failed;
        Schedule? schedule = null;
        string? error = null;

        try
        {
            schedule = await _context.Schedules.AsNoTracking().Include(x => x.Tasks).FirstOrDefaultAsync(x => x.Id == scheduleId, stoppingToken);

            if (schedule == null)
                return ScheduleRunResult.Skipped;

            (result, error) = await RunTasksAsync(schedule, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            error = "The panel was stopped";
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Schedule {ScheduleId} failed", scheduleId);
            error = "Unexpected error";
        }
        finally
        {
            await ReleaseAsync(scheduleId, startedAt);
        }

        if (schedule != null)
        {
            await _activity.LogAsync(schedule.ServerId, ActivityEvents.ScheduleRun, new Dictionary<string, string?>
            {
                ["schedule"] = schedule.Name,
                ["trigger"] = manual ? "manual" : "cron",
                ["result"] = result.ToString().ToLowerInvariant(),
                ["error"] = error
            }, actor);
        }

        return result;
    }

    private async Task<(ScheduleRunResult Result, string? Error)> RunTasksAsync(Schedule schedule, CancellationToken stoppingToken)
    {
        if (schedule.OnlyWhenOnline)
        {
            var server = await LoadServerAsync(schedule.ServerId, stoppingToken);
            var resources = await _serverService.GetResourcesAsync(server, stoppingToken);

            if (resources.State != ServerState.Online)
                return (ScheduleRunResult.Skipped, "The server is not online");
        }

        var failed = false;
        string? firstError = null;

        foreach (var task in schedule.Tasks.OrderBy(x => x.Sequence).ThenBy(x => x.Id))
        {
            if (task.TimeOffset > 0)
                await Task.Delay(TimeSpan.FromSeconds(task.TimeOffset), _timeProvider, stoppingToken);

            try
            {
                // Fresh state for every task: the server may have been suspended or changed during the delays
                var server = await LoadServerAsync(schedule.ServerId, stoppingToken);
                await RunTaskAsync(server, task, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                failed = true;
                firstError ??= $"Task {task.Sequence}: {e.Message}";

                _logger.LogInformation("Task {Sequence} of schedule {ScheduleId} failed: {Error}", task.Sequence, schedule.Id, e.Message);

                if (!task.ContinueOnFailure)
                    break;
            }
        }

        return failed ? (ScheduleRunResult.Failed, firstError) : (ScheduleRunResult.Completed, null);
    }

    private async Task RunTaskAsync(Server server, ScheduleTask task, CancellationToken cancellationToken)
    {
        switch (task.Action)
        {
            case ScheduleTaskAction.Command:
                await _serverService.SendCommandAsync(server, task.Payload, cancellationToken);
                break;

            case ScheduleTaskAction.Power:
                if (!ScheduleService.TryParsePower(task.Payload, out var action))
                    throw new InvalidOperationException($"Invalid power action '{task.Payload}'");

                await _serverService.PowerAsync(server, action, cancellationToken);
                break;

            case ScheduleTaskAction.Backup:
                await _backupService.CreateAsync(server, null, task.Payload, false, cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unknown action {task.Action}");
        }
    }

    private async Task<Server> LoadServerAsync(int serverId, CancellationToken cancellationToken)
    {
        _context.ChangeTracker.Clear();
        return await _serverService.GetAsync(serverId, cancellationToken);
    }

    private async Task ReleaseAsync(int scheduleId, DateTimeOffset startedAt)
    {
        try
        {
            await _context.Schedules
                .Where(x => x.Id == scheduleId)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(s => s.IsProcessing, false)
                    .SetProperty(s => s.LastRunAt, startedAt), CancellationToken.None);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not release schedule {ScheduleId}", scheduleId);
        }
    }
}

/// <summary>
/// Starts schedule runs in the background (singleton). A run is only started when the schedule could be claimed
/// (IsProcessing false → true in one conditional update), so a schedule never runs twice at the same time.
/// </summary>
public class ScheduleDispatcher : IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScheduleDispatcher> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<int, Task> _running = new();

    public ScheduleDispatcher(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<ScheduleDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>False when the schedule is already running (or doesn't exist)</summary>
    /// <param name="manual">Manual runs don't move NextRunAt and also run inactive schedules</param>
    public async Task<bool> TryStartAsync(int scheduleId, bool manual, ActivityActor actor, CancellationToken cancellationToken)
    {
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();

            var claimed = await context.Schedules
                .Where(x => x.Id == scheduleId && !x.IsProcessing)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.IsProcessing, true), cancellationToken);

            if (claimed == 0)
                return false;

            if (!manual)
                await MoveNextRunAsync(scope.ServiceProvider, context, scheduleId);
        }

        Task run;

        // The run must not inherit the http context (activity user, disposed request services) of the caller
        using (ExecutionContext.SuppressFlow())
            run = Task.Run(() => RunInScopeAsync(scheduleId, manual, actor));

        _running[scheduleId] = run;
        _ = run.ContinueWith(_ => _running.TryRemove(new KeyValuePair<int, Task>(scheduleId, run)), TaskScheduler.Default);

        return true;
    }

    /// <summary>Waits for the runs that are in progress (tests and shutdown)</summary>
    public Task WhenIdleAsync() => Task.WhenAll(_running.Values.ToArray());

    /// <summary>Cancels the delays of running schedules and waits a moment for them to release their schedule</summary>
    public async Task StopAsync()
    {
        if (!_stopping.IsCancellationRequested)
            await _stopping.CancelAsync();

        await Task.WhenAny(WhenIdleAsync(), Task.Delay(TimeSpan.FromSeconds(10)));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _stopping.Dispose();
    }

    private async Task MoveNextRunAsync(IServiceProvider services, DataContext context, int scheduleId)
    {
        var schedule = await context.Schedules.FirstAsync(x => x.Id == scheduleId);

        try
        {
            schedule.NextRunAt = services.GetRequiredService<ScheduleService>().GetNextRun(schedule, _timeProvider.GetUtcNow());
        }
        catch (Core.Http.HttpApiException)
        {
            // Invalid cron in the database: stop the schedule instead of failing every 30 seconds
            _logger.LogWarning("Schedule {ScheduleId} has an invalid cron expression and was deactivated", scheduleId);
            schedule.IsActive = false;
            schedule.NextRunAt = null;
        }

        await context.SaveChangesAsync();
    }

    private async Task RunInScopeAsync(int scheduleId, bool manual, ActivityActor actor)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ScheduleRunner>().RunAsync(scheduleId, manual, actor, _stopping.Token);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Schedule {ScheduleId} could not be run", scheduleId);
        }
    }
}

/// <summary>Checks every 30 seconds for due schedules and hands them to the dispatcher</summary>
public class ScheduleWorker : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>Runs missed by more than this while the panel was down are skipped</summary>
    public static readonly TimeSpan MissedRunTolerance = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScheduleDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScheduleWorker> _logger;

    public ScheduleWorker(IServiceScopeFactory scopeFactory, ScheduleDispatcher dispatcher, TimeProvider timeProvider, ILogger<ScheduleWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ResetAsync(stoppingToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Could not reset the schedules");
        }

        using var timer = new PeriodicTimer(Interval, _timeProvider);

        do
        {
            try
            {
                await RunDueAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(e, "Checking the schedules failed");
            }
        } while (await WaitAsync(timer, stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await _dispatcher.StopAsync();
    }

    /// <summary>
    /// After a restart nothing is running anymore: IsProcessing is reset. Runs that were missed by more than
    /// <see cref="MissedRunTolerance"/> are skipped (NextRunAt recalculated), slightly late ones still run.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var schedules = scope.ServiceProvider.GetRequiredService<ScheduleService>();

        await context.Schedules.Where(x => x.IsProcessing).ExecuteUpdateAsync(x => x.SetProperty(s => s.IsProcessing, false), cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var threshold = now - MissedRunTolerance;

        var stale = await context.Schedules
            .Where(x => x.IsActive && (x.NextRunAt == null || x.NextRunAt < threshold))
            .ToListAsync(cancellationToken);

        foreach (var schedule in stale)
        {
            try
            {
                schedule.NextRunAt = schedules.GetNextRun(schedule, now);
            }
            catch (Core.Http.HttpApiException)
            {
                schedule.IsActive = false;
                schedule.NextRunAt = null;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Starts every active schedule whose NextRunAt has passed</summary>
    public async Task RunDueAsync(CancellationToken cancellationToken)
    {
        List<int> due;
        var now = _timeProvider.GetUtcNow();

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<DataContext>().Schedules
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsProcessing && x.NextRunAt != null && x.NextRunAt <= now)
                .OrderBy(x => x.NextRunAt)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        foreach (var id in due)
        {
            try
            {
                await _dispatcher.TryStartAsync(id, manual: false, ActivityActor.System, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(e, "Could not start schedule {ScheduleId}", id);
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
