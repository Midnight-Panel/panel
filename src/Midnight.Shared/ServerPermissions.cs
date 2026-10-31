namespace Midnight.Shared;

/// <summary>
/// Permissions of a user on a single server (subusers). The owner and admins with admin.servers.write
/// have all of them, shown as "*". Subusers only get exactly the listed strings (no wildcards).
/// </summary>
public static class ServerPermissions
{
    public const string All = "*";

    public const string WebsocketConnect = "websocket.connect";

    public const string ControlConsole = "control.console";
    public const string ControlStart = "control.start";
    public const string ControlStop = "control.stop";
    public const string ControlRestart = "control.restart";

    public const string UserRead = "user.read";
    public const string UserCreate = "user.create";
    public const string UserUpdate = "user.update";
    public const string UserDelete = "user.delete";

    public const string FileRead = "file.read";
    public const string FileReadContent = "file.read-content";
    public const string FileCreate = "file.create";
    public const string FileUpdate = "file.update";
    public const string FileDelete = "file.delete";
    public const string FileArchive = "file.archive";

    public const string BackupRead = "backup.read";
    public const string BackupCreate = "backup.create";
    public const string BackupDelete = "backup.delete";
    public const string BackupDownload = "backup.download";
    public const string BackupRestore = "backup.restore";

    public const string AllocationRead = "allocation.read";
    public const string AllocationCreate = "allocation.create";
    public const string AllocationUpdate = "allocation.update";
    public const string AllocationDelete = "allocation.delete";

    public const string StartupRead = "startup.read";
    public const string StartupUpdate = "startup.update";

    public const string DatabaseRead = "database.read";
    public const string DatabaseCreate = "database.create";
    public const string DatabaseUpdate = "database.update";
    public const string DatabaseDelete = "database.delete";
    public const string DatabaseViewPassword = "database.view-password";

    public const string ScheduleRead = "schedule.read";
    public const string ScheduleCreate = "schedule.create";
    public const string ScheduleUpdate = "schedule.update";
    public const string ScheduleDelete = "schedule.delete";

    public const string SettingsRename = "settings.rename";
    public const string SettingsReinstall = "settings.reinstall";

    public const string ActivityRead = "activity.read";

    /// <summary>Grouped with descriptions for the subuser editor</summary>
    public static readonly ServerPermissionGroup[] Groups =
    [
        new("control", "Console and power", [
            new(WebsocketConnect, "View the console and live stats"),
            new(ControlConsole, "Send commands to the console"),
            new(ControlStart, "Start the server"),
            new(ControlStop, "Stop and kill the server"),
            new(ControlRestart, "Restart the server")
        ]),
        new("user", "Users", [
            new(UserRead, "See the users of this server"),
            new(UserCreate, "Invite users"),
            new(UserUpdate, "Change permissions of users"),
            new(UserDelete, "Remove users")
        ]),
        new("file", "Files", [
            new(FileRead, "List files and folders"),
            new(FileReadContent, "Open and download files"),
            new(FileCreate, "Create and upload files"),
            new(FileUpdate, "Edit, rename, move and chmod files"),
            new(FileDelete, "Delete files"),
            new(FileArchive, "Compress and extract archives")
        ]),
        new("backup", "Backups", [
            new(BackupRead, "See backups"),
            new(BackupCreate, "Create backups"),
            new(BackupDelete, "Delete backups"),
            new(BackupDownload, "Download backups"),
            new(BackupRestore, "Restore backups (overwrites files)")
        ]),
        new("allocation", "Network", [
            new(AllocationRead, "See ports"),
            new(AllocationCreate, "Add ports"),
            new(AllocationUpdate, "Change notes and the primary port"),
            new(AllocationDelete, "Remove ports")
        ]),
        new("startup", "Startup", [
            new(StartupRead, "See startup variables"),
            new(StartupUpdate, "Change startup variables")
        ]),
        new("database", "Databases", [
            new(DatabaseRead, "See databases"),
            new(DatabaseCreate, "Create databases"),
            new(DatabaseUpdate, "Rotate passwords"),
            new(DatabaseDelete, "Delete databases"),
            new(DatabaseViewPassword, "See database passwords")
        ]),
        new("schedule", "Schedules", [
            new(ScheduleRead, "See schedules"),
            new(ScheduleCreate, "Create schedules"),
            new(ScheduleUpdate, "Change and run schedules"),
            new(ScheduleDelete, "Delete schedules")
        ]),
        new("settings", "Settings", [
            new(SettingsRename, "Rename the server"),
            new(SettingsReinstall, "Reinstall the server")
        ]),
        new("activity", "Activity", [
            new(ActivityRead, "See the activity log")
        ])
    ];

    public static IEnumerable<string> AllPermissions => Groups.SelectMany(g => g.Permissions.Select(p => p.Key));

    public static bool IsValid(string permission) => AllPermissions.Contains(permission);

    public static bool Has(IEnumerable<string> granted, string required)
        => granted.Any(p => p == All || p == required);
}

public record ServerPermissionGroup(string Key, string Name, ServerPermissionInfo[] Permissions);

public record ServerPermissionInfo(string Key, string Description);
