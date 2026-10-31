using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Http;

/// <summary>
/// Schedules of a server. Tasks can only do what the user may do themselves: command tasks need control.console,
/// power tasks the permission of the action and backup tasks backup.create (403 otherwise).
/// </summary>
[ApiController]
[Route("api/servers/{uuid:guid}/schedules")]
[RequirePermission(Permissions.AccountRead)]
public class ServerSchedulesController : ControllerBase
{
    private readonly ServerAccessService _access;
    private readonly ScheduleService _schedules;
    private readonly ScheduleDispatcher _dispatcher;
    private readonly ActivityService _activity;

    public ServerSchedulesController(ServerAccessService access, ScheduleService schedules, ScheduleDispatcher dispatcher, ActivityService activity)
    {
        _access = access;
        _schedules = schedules;
        _dispatcher = dispatcher;
        _activity = activity;
    }

    [HttpGet]
    public async Task<List<ScheduleResponse>> List(Guid uuid, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ScheduleRead, cancellationToken);
        return (await _schedules.ListAsync(access.Server, cancellationToken)).Select(ScheduleService.ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ScheduleResponse> Get(Guid uuid, int id, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ScheduleRead, cancellationToken);
        return ScheduleService.ToResponse(await _schedules.GetAsync(access.Server, id, cancellationToken));
    }

    /// <summary>Invalid cron parts: 400 with errors per field (Minute, Hour, DayOfMonth, Month, DayOfWeek)</summary>
    [HttpPost]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ScheduleResponse> Create(Guid uuid, [FromBody] ScheduleRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ScheduleCreate, cancellationToken);
        var schedule = await _schedules.CreateAsync(access.Server, request, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.ScheduleCreate, Describe(schedule));

        return ScheduleService.ToResponse(schedule);
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ScheduleResponse> Update(Guid uuid, int id, [FromBody] ScheduleRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ScheduleUpdate, cancellationToken);
        var schedule = await _schedules.UpdateAsync(access.Server, id, request, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.ScheduleUpdate, Describe(schedule));

        return ScheduleService.ToResponse(schedule);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Delete(Guid uuid, int id, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ScheduleDelete, cancellationToken);
        var schedule = await _schedules.DeleteAsync(access.Server, id, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.ScheduleDelete, Describe(schedule));

        return NoContent();
    }

    /// <summary>Runs the schedule now (also inactive ones), 409 while it is already running</summary>
    [HttpPost("{id:int}/execute")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> Execute(Guid uuid, int id, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ScheduleUpdate, cancellationToken);
        var schedule = await _schedules.GetAsync(access.Server, id, cancellationToken);

        if (!await _dispatcher.TryStartAsync(schedule.Id, manual: true, _activity.GetCurrentActor(), cancellationToken))
            throw HttpApiException.Conflict("The schedule is already running");

        await _activity.LogAsync(access.Server.Id, ActivityEvents.ScheduleExecute, Describe(schedule));

        return Accepted();
    }

    /// <summary>At most 10 tasks (409)</summary>
    [HttpPost("{id:int}/tasks")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ScheduleTaskResponse> CreateTask(Guid uuid, int id, [FromBody] ScheduleTaskRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ScheduleUpdate, cancellationToken);
        EnsureTaskAllowed(access, request);

        var task = await _schedules.AddTaskAsync(access.Server, id, request, cancellationToken);
        await _activity.LogAsync(access.Server.Id, ActivityEvents.ScheduleUpdate, DescribeTask(id, task));

        return ScheduleService.ToResponse(task);
    }

    [HttpPut("{id:int}/tasks/{taskId:int}")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<ScheduleTaskResponse> UpdateTask(Guid uuid, int id, int taskId, [FromBody] ScheduleTaskRequest request, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ScheduleUpdate, cancellationToken);
        EnsureTaskAllowed(access, request);

        var task = await _schedules.UpdateTaskAsync(access.Server, id, taskId, request, cancellationToken);
        await _activity.LogAsync(access.Server.Id, ActivityEvents.ScheduleUpdate, DescribeTask(id, task));

        return ScheduleService.ToResponse(task);
    }

    [HttpDelete("{id:int}/tasks/{taskId:int}")]
    [RequirePermission(Permissions.AccountWrite)]
    public async Task<IActionResult> DeleteTask(Guid uuid, int id, int taskId, CancellationToken cancellationToken)
    {
        var access = await _access.GetAsync(User, uuid, ServerPermissions.ScheduleUpdate, cancellationToken);
        var task = await _schedules.DeleteTaskAsync(access.Server, id, taskId, cancellationToken);

        await _activity.LogAsync(access.Server.Id, ActivityEvents.ScheduleUpdate, DescribeTask(id, task));

        return NoContent();
    }

    /// <summary>A task may only do what the user may do directly</summary>
    internal static void EnsureTaskAllowed(ServerAccessContext access, ScheduleTaskRequest request)
    {
        switch (request.Action)
        {
            case ScheduleTaskAction.Command:
                access.Ensure(ServerPermissions.ControlConsole);
                break;

            case ScheduleTaskAction.Power:
                if (ScheduleService.TryParsePower(request.Payload?.Trim(), out var power))
                    access.Ensure(ServerAccess.GetPowerPermission(power));
                break;

            case ScheduleTaskAction.Backup:
                access.Ensure(ServerPermissions.BackupCreate);
                break;
        }
    }

    private static Dictionary<string, string?> Describe(Schedule schedule) => new() { ["schedule"] = schedule.Name };

    private static Dictionary<string, string?> DescribeTask(int scheduleId, ScheduleTask task) => new()
    {
        ["schedule"] = scheduleId.ToString(),
        ["task"] = task.Sequence.ToString(),
        ["action"] = task.Action.ToString()
    };
}
