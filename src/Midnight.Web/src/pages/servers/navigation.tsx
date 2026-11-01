import { ArchiveIcon, BoxIcon, CalendarIcon, CodeIcon, DatabaseIcon, FolderIcon, GearIcon, GlobeIcon, GroupIcon, ListIcon } from "@/icons";
import { ServerPermissions } from "@/lib/server-permissions";
import type { ReactNode } from "react";

export interface ServerNavEntry {
  /** Translation key of the label */
  key: string;
  icon: ReactNode;
  path: string;
  /** Read permission needed to see the page, none = everyone with access */
  permission?: string;
}

/** Pages of a server, used by the tabs of the server layout and the server section of the sidebar */
export function serverNavigation(uuid: string): ServerNavEntry[] {
  const base = `/servers/${uuid}`;

  return [
    { key: "Servers.Tabs.Console", path: base, icon: <CodeIcon /> },
    { key: "Servers.Tabs.Files", path: `${base}/files`, icon: <FolderIcon />, permission: ServerPermissions.FileRead },
    { key: "Servers.Tabs.Databases", path: `${base}/databases`, icon: <DatabaseIcon />, permission: ServerPermissions.DatabaseRead },
    { key: "Servers.Tabs.Schedules", path: `${base}/schedules`, icon: <CalendarIcon />, permission: ServerPermissions.ScheduleRead },
    { key: "Servers.Tabs.Users", path: `${base}/users`, icon: <GroupIcon />, permission: ServerPermissions.UserRead },
    { key: "Servers.Tabs.Backups", path: `${base}/backups`, icon: <ArchiveIcon />, permission: ServerPermissions.BackupRead },
    { key: "Servers.Tabs.Network", path: `${base}/network`, icon: <GlobeIcon />, permission: ServerPermissions.AllocationRead },
    { key: "Servers.Tabs.Startup", path: `${base}/startup`, icon: <BoxIcon />, permission: ServerPermissions.StartupRead },
    { key: "Servers.Tabs.Settings", path: `${base}/settings`, icon: <GearIcon /> },
    { key: "Servers.Tabs.Activity", path: `${base}/activity`, icon: <ListIcon />, permission: ServerPermissions.ActivityRead },
  ];
}

/** "/servers/<uuid>/..." → uuid */
export function serverUuidFromPath(pathname: string) {
  return /^\/servers\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})(?:\/|$)/i.exec(pathname)?.[1];
}
