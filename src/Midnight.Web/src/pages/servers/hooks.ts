import type { PowerAction, ServerDetail, ServerState, ServerStats } from "@/api/types";
import { ServerPermissions } from "@/lib/server-permissions";
import { useCallback, useEffect, useState, useSyncExternalStore } from "react";
import { useOutletContext } from "react-router";
import { ConsoleConnection, type ConsoleSnapshot } from "./console-connection";

export interface ServerLive extends ConsoleSnapshot {
  /** Websocket state, falls back to the polled resources */
  state?: ServerState;
  stats?: ServerStats;
  nodeOnline: boolean;
}

export interface ServerContextValue {
  server: ServerDetail;
  live: ServerLive;
  connection: ConsoleConnection;
  /**
   * Server permission of the current user (see lib/server-permissions, "*" = all). Permissions that change
   * something additionally need account.write, like before.
   */
  can: (permission: string) => boolean;
  power: (action: PowerAction) => Promise<void>;
  sendCommand: (command: string) => Promise<boolean>;
}

/** The server of the /servers/:uuid layout */
export function useServerContext() {
  return useOutletContext<ServerContextValue>();
}

export function serverQueryKey(uuid: string) {
  return ["servers", uuid] as const;
}

/** Permission of each power action (kill counts as stop) */
export const powerPermissions: Record<PowerAction, string> = {
  Start: ServerPermissions.ControlStart,
  Stop: ServerPermissions.ControlStop,
  Kill: ServerPermissions.ControlStop,
  Restart: ServerPermissions.ControlRestart,
};

/**
 * One console websocket per opened server, shared by the header and the tabs.
 * Only connects while enabled (the user has websocket.connect).
 */
export function useConsoleConnection(uuid: string, enabled = true) {
  const [connection] = useState(() => new ConsoleConnection(uuid));

  const subscribe = useCallback((listener: () => void) => connection.subscribe(listener), [connection]);
  const getSnapshot = useCallback(() => connection.getSnapshot(), [connection]);
  const snapshot = useSyncExternalStore(subscribe, getSnapshot);

  useEffect(() => {
    if (!enabled) return;

    connection.start();
    return () => connection.stop();
  }, [connection, enabled]);

  return { connection, snapshot };
}
