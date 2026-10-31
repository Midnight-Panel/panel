using Midnight.Shared.Http.Servers;

namespace Midnight.ApiServer.Features.Servers.Entities;

/// <summary>Runs its tasks when the cron expression (panel time zone) is due</summary>
public class Schedule
{
    public int Id { get; set; }

    public int ServerId { get; set; }
    public Server Server { get; set; } = null!;

    public string Name { get; set; } = "";

    public string Minute { get; set; } = "*";
    public string Hour { get; set; } = "*";
    public string DayOfMonth { get; set; } = "*";
    public string Month { get; set; } = "*";
    public string DayOfWeek { get; set; } = "*";

    public bool IsActive { get; set; } = true;

    /// <summary>Skip the run when the server is not online</summary>
    public bool OnlyWhenOnline { get; set; }

    /// <summary>Set while a run is in progress, guards against double runs. Reset on panel start.</summary>
    public bool IsProcessing { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>null for inactive schedules</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<ScheduleTask> Tasks { get; set; } = [];
}

public class ScheduleTask
{
    public int Id { get; set; }

    public int ScheduleId { get; set; }
    public Schedule Schedule { get; set; } = null!;

    /// <summary>1 based order, re-numbered when a task is deleted</summary>
    public int Sequence { get; set; }

    public ScheduleTaskAction Action { get; set; }

    /// <summary>Command: the command. Power: Start/Stop/Restart/Kill. Backup: ignored files.</summary>
    public string Payload { get; set; } = "";

    /// <summary>Seconds to wait after the previous task</summary>
    public int TimeOffset { get; set; }

    public bool ContinueOnFailure { get; set; }
}
