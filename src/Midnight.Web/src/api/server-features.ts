import { api } from "./client";
import type { PagedResult, ServerAllocation } from "./types";

// Mirror of Midnight.Shared.Http.Servers.ServerFeatureModels (camelCase json, dates as ISO strings)
// and ServerPermissions.Groups. Every endpoint needs the server permission named in its comment.

// ─── Permissions ────────────────────────────────────────────────────────────

export interface ServerPermissionInfo {
  key: string;
  description: string;
}

export interface ServerPermissionGroup {
  key: string;
  name: string;
  permissions: ServerPermissionInfo[];
}

// ─── Subusers ───────────────────────────────────────────────────────────────

export interface Subuser {
  id: number;
  userId: number;
  username: string;
  email: string;
  hasAvatar: boolean;
  permissions: string[];
  createdAt: string;
}

export interface CreateSubuserRequest {
  /** Email of an existing panel account */
  email: string;
  permissions: string[];
}

export interface UpdateSubuserRequest {
  permissions: string[];
}

// ─── Backups ────────────────────────────────────────────────────────────────

export interface Backup {
  uuid: string;
  name: string;
  /** gitignore style lines */
  ignoredFiles: string;
  checksum?: string | null;
  bytes: number;
  isSuccessful: boolean;
  isLocked: boolean;
  /** null while the backup is still running */
  completedAt?: string | null;
  createdAt: string;
}

export interface BackupList {
  backups: Backup[];
  /** FeatureLimits.Backups of the server, 0 = disabled */
  limit: number;
}

export interface CreateBackupRequest {
  /** null = "Backup at {date}" */
  name?: string | null;
  ignoredFiles?: string | null;
  isLocked: boolean;
}

export interface RestoreBackupRequest {
  /** Delete all files before restoring */
  truncate: boolean;
}

/** Single use download link, valid for 60 seconds */
export interface DownloadTicket {
  url: string;
}

// ─── Network ────────────────────────────────────────────────────────────────

export interface ServerAllocationList {
  allocations: ServerAllocation[];
  /** How many allocations besides the primary one the server may have */
  limit: number;
}

// ─── Schedules ──────────────────────────────────────────────────────────────

export type ScheduleTaskAction = "Command" | "Power" | "Backup";

export interface ScheduleTask {
  id: number;
  sequence: number;
  action: ScheduleTaskAction;
  /** Command: the command. Power: Start/Stop/Restart/Kill. Backup: ignored files (optional) */
  payload: string;
  /** Seconds to wait after the previous task */
  timeOffset: number;
  continueOnFailure: boolean;
}

export interface CronParts {
  minute: string;
  hour: string;
  dayOfMonth: string;
  month: string;
  dayOfWeek: string;
}

export interface Schedule extends CronParts {
  id: number;
  name: string;
  isActive: boolean;
  /** Skip the run when the server is not online */
  onlyWhenOnline: boolean;
  isProcessing: boolean;
  lastRunAt?: string | null;
  nextRunAt?: string | null;
  tasks: ScheduleTask[];
}

export interface ScheduleRequest extends CronParts {
  name: string;
  isActive: boolean;
  onlyWhenOnline: boolean;
}

export interface ScheduleTaskRequest {
  action: ScheduleTaskAction;
  payload: string;
  /** 0-900 seconds */
  timeOffset: number;
  continueOnFailure: boolean;
}

// ─── Databases ──────────────────────────────────────────────────────────────

export interface ServerDatabase {
  id: number;
  /** Full database name, e.g. s12_minecraft */
  name: string;
  username: string;
  /** Only with database.view-password, otherwise null */
  password?: string | null;
  /** Allowed connection source, "%" = anywhere */
  remote: string;
  host: string;
  port: number;
  createdAt: string;
}

export interface ServerDatabaseList {
  databases: ServerDatabase[];
  limit: number;
}

export interface CreateServerDatabaseRequest {
  /** Suffix, the panel prefixes it with s{serverId}_ */
  database: string;
  remote: string;
}

// ─── Activity ───────────────────────────────────────────────────────────────

export interface ServerActivity {
  id: number;
  /** e.g. "server:power.start", "server:file.write", "server:subuser.create" */
  event: string;
  /** Short context like file names or the command, already safe to show */
  properties: Record<string, string>;
  userId?: number | null;
  username?: string | null;
  ipAddress?: string | null;
  createdAt: string;
}

// ─── Api ────────────────────────────────────────────────────────────────────

const base = (uuid: string) => `/api/servers/${uuid}`;

export const serverPermissionsApi = {
  /** Static list for the subuser editor */
  groups: () => api.get<ServerPermissionGroup[]>("/api/servers/permissions"),
};

export const subusersApi = {
  /** user.read */
  list: (uuid: string) => api.get<Subuser[]>(`${base(uuid)}/users`),
  /** user.create */
  create: (uuid: string, data: CreateSubuserRequest) => api.post<Subuser>(`${base(uuid)}/users`, data),
  /** user.update */
  update: (uuid: string, id: number, data: UpdateSubuserRequest) => api.put<Subuser>(`${base(uuid)}/users/${id}`, data),
  /** user.delete */
  delete: (uuid: string, id: number) => api.delete(`${base(uuid)}/users/${id}`),
};

export const backupsApi = {
  /** backup.read */
  list: (uuid: string) => api.get<BackupList>(`${base(uuid)}/backups`),
  /** backup.create */
  create: (uuid: string, data: CreateBackupRequest) => api.post<Backup>(`${base(uuid)}/backups`, data),
  /** backup.delete – toggles the lock */
  toggleLock: (uuid: string, backup: string) => api.post<Backup>(`${base(uuid)}/backups/${backup}/lock`),
  /** backup.download */
  downloadTicket: (uuid: string, backup: string) => api.post<DownloadTicket>(`${base(uuid)}/backups/${backup}/download-ticket`),
  /** backup.restore */
  restore: (uuid: string, backup: string, data: RestoreBackupRequest) => api.post(`${base(uuid)}/backups/${backup}/restore`, data),
  /** backup.delete */
  delete: (uuid: string, backup: string) => api.delete(`${base(uuid)}/backups/${backup}`),
};

export const serverAllocationsApi = {
  /** allocation.read */
  list: (uuid: string) => api.get<ServerAllocationList>(`${base(uuid)}/allocations`),
  /** allocation.create – a random free allocation of the node */
  create: (uuid: string) => api.post<ServerAllocation>(`${base(uuid)}/allocations`),
  /** allocation.update */
  updateNotes: (uuid: string, id: number, notes: string | null) =>
    api.put<ServerAllocation>(`${base(uuid)}/allocations/${id}/notes`, { notes }),
  /** allocation.update */
  makePrimary: (uuid: string, id: number) => api.post<ServerAllocation>(`${base(uuid)}/allocations/${id}/primary`),
  /** allocation.delete */
  delete: (uuid: string, id: number) => api.delete(`${base(uuid)}/allocations/${id}`),
};

export const schedulesApi = {
  /** schedule.read */
  list: (uuid: string) => api.get<Schedule[]>(`${base(uuid)}/schedules`),
  /** schedule.read */
  get: (uuid: string, id: number) => api.get<Schedule>(`${base(uuid)}/schedules/${id}`),
  /** schedule.create */
  create: (uuid: string, data: ScheduleRequest) => api.post<Schedule>(`${base(uuid)}/schedules`, data),
  /** schedule.update */
  update: (uuid: string, id: number, data: ScheduleRequest) => api.put<Schedule>(`${base(uuid)}/schedules/${id}`, data),
  /** schedule.delete */
  delete: (uuid: string, id: number) => api.delete(`${base(uuid)}/schedules/${id}`),
  /** schedule.update – run now */
  execute: (uuid: string, id: number) => api.post(`${base(uuid)}/schedules/${id}/execute`),
  /** schedule.update */
  createTask: (uuid: string, id: number, data: ScheduleTaskRequest) =>
    api.post<ScheduleTask>(`${base(uuid)}/schedules/${id}/tasks`, data),
  /** schedule.update */
  updateTask: (uuid: string, id: number, taskId: number, data: ScheduleTaskRequest) =>
    api.put<ScheduleTask>(`${base(uuid)}/schedules/${id}/tasks/${taskId}`, data),
  /** schedule.update */
  deleteTask: (uuid: string, id: number, taskId: number) => api.delete(`${base(uuid)}/schedules/${id}/tasks/${taskId}`),
};

export const serverDatabasesApi = {
  /** database.read (passwords only with database.view-password) */
  list: (uuid: string) => api.get<ServerDatabaseList>(`${base(uuid)}/databases`),
  /** database.create */
  create: (uuid: string, data: CreateServerDatabaseRequest) => api.post<ServerDatabase>(`${base(uuid)}/databases`, data),
  /** database.update */
  rotatePassword: (uuid: string, id: number) => api.post<ServerDatabase>(`${base(uuid)}/databases/${id}/rotate-password`),
  /** database.delete */
  delete: (uuid: string, id: number) => api.delete(`${base(uuid)}/databases/${id}`),
};

export const serverActivityApi = {
  /** activity.read */
  list: (uuid: string, page: number, pageSize: number) =>
    api.get<PagedResult<ServerActivity>>(`${base(uuid)}/activity?page=${page}&pageSize=${pageSize}`),
};

/** Query keys of the feature tabs, below ["servers", uuid] */
export const serverFeatureKeys = {
  subusers: (uuid: string) => ["servers", uuid, "users"] as const,
  backups: (uuid: string) => ["servers", uuid, "backups"] as const,
  allocations: (uuid: string) => ["servers", uuid, "allocations"] as const,
  schedules: (uuid: string) => ["servers", uuid, "schedules"] as const,
  schedule: (uuid: string, id: number) => ["servers", uuid, "schedules", id] as const,
  databases: (uuid: string) => ["servers", uuid, "databases"] as const,
  activity: (uuid: string) => ["servers", uuid, "activity"] as const,
};

/** Starts a browser download of a ticket url (the bearer token can't be sent with a plain link) */
export function startDownload(url: string) {
  const link = document.createElement("a");
  link.href = url;
  link.rel = "noopener";
  document.body.appendChild(link);
  link.click();
  link.remove();
}
