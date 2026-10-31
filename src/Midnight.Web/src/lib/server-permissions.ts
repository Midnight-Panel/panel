// Mirror of Midnight.Shared.ServerPermissions (permissions of a user on one server, subusers).
// The api enforces them, the ui only hides what isn't allowed.

export const ServerPermissions = {
  All: "*",

  WebsocketConnect: "websocket.connect",

  ControlConsole: "control.console",
  ControlStart: "control.start",
  ControlStop: "control.stop",
  ControlRestart: "control.restart",

  UserRead: "user.read",
  UserCreate: "user.create",
  UserUpdate: "user.update",
  UserDelete: "user.delete",

  FileRead: "file.read",
  FileReadContent: "file.read-content",
  FileCreate: "file.create",
  FileUpdate: "file.update",
  FileDelete: "file.delete",
  FileArchive: "file.archive",

  BackupRead: "backup.read",
  BackupCreate: "backup.create",
  BackupDelete: "backup.delete",
  BackupDownload: "backup.download",
  BackupRestore: "backup.restore",

  AllocationRead: "allocation.read",
  AllocationCreate: "allocation.create",
  AllocationUpdate: "allocation.update",
  AllocationDelete: "allocation.delete",

  StartupRead: "startup.read",
  StartupUpdate: "startup.update",

  DatabaseRead: "database.read",
  DatabaseCreate: "database.create",
  DatabaseUpdate: "database.update",
  DatabaseDelete: "database.delete",
  DatabaseViewPassword: "database.view-password",

  ScheduleRead: "schedule.read",
  ScheduleCreate: "schedule.create",
  ScheduleUpdate: "schedule.update",
  ScheduleDelete: "schedule.delete",

  SettingsRename: "settings.rename",
  SettingsReinstall: "settings.reinstall",

  ActivityRead: "activity.read",
} as const;

export type ServerPermission = (typeof ServerPermissions)[keyof typeof ServerPermissions];

/** Same rule as ServerPermissions.Has: "*" grants everything, otherwise exact matches only. Missing list = nothing. */
export function hasServerPermission(granted: readonly string[] | undefined, required: string) {
  return (granted ?? []).some((permission) => permission === ServerPermissions.All || permission === required);
}

/** Permissions an admin with only admin.servers.read gets – they never change anything */
export function isReadServerPermission(permission: string) {
  return (
    permission.endsWith(".read") ||
    permission === ServerPermissions.FileReadContent ||
    permission === ServerPermissions.WebsocketConnect ||
    permission === ServerPermissions.ActivityRead
  );
}
