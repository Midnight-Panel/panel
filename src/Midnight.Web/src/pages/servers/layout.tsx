import { serversApi } from "@/api/servers";
import { useAuth } from "@/auth/AuthContext";
import type { PowerAction } from "@/api/types";
import { PageLoader } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { TabNav, type TabNavItem } from "@/components/shared/tab-nav";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { useT } from "@/i18n";
import { ArchiveIcon, BoxIcon, CalendarIcon, ChipIcon, CodeIcon, DatabaseIcon, EyeIcon, FolderIcon, GearIcon, GlobeIcon, GroupIcon, ListIcon, OctagonAlertIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { hasServerPermission, isReadServerPermission, ServerPermissions } from "@/lib/server-permissions";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { Outlet, useNavigate, useParams } from "react-router";
import { toast } from "sonner";
import { CopyButton, ServerStateBadge, ServerStatusBadge } from "./components/badges";
import { PowerButtons } from "./components/power-buttons";
import { powerPermissions, serverQueryKey, useConsoleConnection, type ServerContextValue, type ServerLive } from "./hooks";

function ServerContent({ uuid }: { uuid: string }) {
  const { t } = useT();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { can } = useAuth();
  const server = useQuery({ queryKey: serverQueryKey(uuid), queryFn: () => serversApi.get(uuid) });
  const canConnect = !!server.data && hasServerPermission(server.data.permissions, ServerPermissions.WebsocketConnect);
  const { connection, snapshot } = useConsoleConnection(uuid, canConnect);

  // Fallback while the websocket is not connected
  const resources = useQuery({
    queryKey: ["servers", uuid, "resources"],
    queryFn: () => serversApi.resources(uuid),
    refetchInterval: snapshot.connection === "connected" ? false : 10_000,
  });

  useEffect(() => {
    connection.setHandlers({
      onError: (message) => toast.error(message),
      onInstallCompleted: () => void queryClient.invalidateQueries({ queryKey: serverQueryKey(uuid) }),
    });

    return () => connection.setHandlers({});
  }, [connection, queryClient, uuid]);

  useEffect(() => {
    if (server.error) {
      notifyError(server.error);
      navigate("/");
    }
  }, [server.error, navigate]);

  if (!server.data) return <PageLoader />;

  const data = server.data;
  const base = `/servers/${uuid}`;
  const connected = snapshot.connection === "connected";

  const live: ServerLive = {
    ...snapshot,
    state: snapshot.state ?? resources.data?.state,
    stats: snapshot.stats ?? resources.data?.stats,
    nodeOnline: connected || (resources.data?.nodeOnline ?? true),
  };

  const accountWrite = can(Permissions.AccountWrite);
  // Changing something additionally needs account.write (read only api keys / sessions)
  const canServer = (permission: string) =>
    hasServerPermission(data.permissions, permission) && (accountWrite || isReadServerPermission(permission));
  const readOnly = !(data.permissions ?? []).some((permission) => permission === ServerPermissions.All || !isReadServerPermission(permission)) || !accountWrite;
  const powerAllowed = (Object.keys(powerPermissions) as PowerAction[]).filter((action) => canServer(powerPermissions[action]));
  const installing = live.installing || data.status === "Installing" || live.state === "Installing";

  const power = async (action: PowerAction) => {
    try {
      await serversApi.power(uuid, action);
      notifySuccess(t(`Servers.Power.Sent${action}`));
      if (!connected) await queryClient.invalidateQueries({ queryKey: ["servers", uuid, "resources"] });
    } catch (error) {
      notifyError(error);
    }
  };

  const sendCommand = async (command: string) => {
    if (connection.send({ event: "command", data: command })) return true;

    try {
      await serversApi.command(uuid, command);
      return true;
    } catch (error) {
      notifyError(error);
      return false;
    }
  };

  const context: ServerContextValue = { server: data, live, connection, can: canServer, power, sendCommand };

  const tabs: Array<TabNavItem & { permission?: string }> = [
    { label: t("Servers.Tabs.Console"), to: base, icon: <CodeIcon /> },
    { label: t("Servers.Tabs.Files"), to: `${base}/files`, icon: <FolderIcon />, permission: ServerPermissions.FileRead },
    { label: t("Servers.Tabs.Databases"), to: `${base}/databases`, icon: <DatabaseIcon />, permission: ServerPermissions.DatabaseRead },
    { label: t("Servers.Tabs.Schedules"), to: `${base}/schedules`, icon: <CalendarIcon />, permission: ServerPermissions.ScheduleRead },
    { label: t("Servers.Tabs.Users"), to: `${base}/users`, icon: <GroupIcon />, permission: ServerPermissions.UserRead },
    { label: t("Servers.Tabs.Backups"), to: `${base}/backups`, icon: <ArchiveIcon />, permission: ServerPermissions.BackupRead },
    { label: t("Servers.Tabs.Network"), to: `${base}/network`, icon: <GlobeIcon />, permission: ServerPermissions.AllocationRead },
    { label: t("Servers.Tabs.Startup"), to: `${base}/startup`, icon: <BoxIcon />, permission: ServerPermissions.StartupRead },
    { label: t("Servers.Tabs.Settings"), to: `${base}/settings`, icon: <GearIcon /> },
    { label: t("Servers.Tabs.Activity"), to: `${base}/activity`, icon: <ListIcon />, permission: ServerPermissions.ActivityRead },
  ];

  return (
    <>
      <PageHeader
        icon={<ChipIcon />}
        title={data.name}
        crumbs={[{ label: t("Nav.Dashboard"), to: "/" }]}
        description={
          <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
            {data.primaryAllocation && (
              <span className="inline-flex items-center gap-1 font-mono text-gray-700 dark:text-gray-300">
                {data.primaryAllocation}
                <CopyButton text={data.primaryAllocation} />
              </span>
            )}
            <span>{data.shardName} · {data.nodeName}</span>
          </span>
        }
        actions={
          <>
            {data.suspended || data.status !== "Ready"
              ? <ServerStatusBadge status={data.status} suspended={data.suspended} />
              : <ServerStateBadge state={live.state} nodeOnline={live.nodeOnline} />}
            {powerAllowed.length > 0 && (
              <PowerButtons
                state={live.state}
                allowed={powerAllowed}
                blocked={data.suspended || installing || data.status === "InstallFailed" || !live.nodeOnline}
                onPower={power}
              />
            )}
          </>
        }
      />

      {readOnly && (
        <Alert variant="info" className="mb-6 px-4 py-3">
          <EyeIcon />
          <AlertTitle>{t("Servers.ReadOnlyAccess")}</AlertTitle>
          <AlertDescription>{t("Servers.ReadOnlyAccessHint")}</AlertDescription>
        </Alert>
      )}
      {data.suspended && (
        <Alert variant="warning" className="mb-6 px-4 py-3">
          <OctagonAlertIcon />
          <AlertTitle>{t("Servers.Status.Suspended")}</AlertTitle>
          <AlertDescription>{t("Servers.SuspendedUserHint")}</AlertDescription>
        </Alert>
      )}
      {data.status === "InstallFailed" && !installing && (
        <Alert variant="destructive" className="mb-6 px-4 py-3">
          <OctagonAlertIcon />
          <AlertTitle>{t("Servers.Status.InstallFailed")}</AlertTitle>
          <AlertDescription>{t("Servers.InstallFailedUserHint")}</AlertDescription>
        </Alert>
      )}

      <TabNav items={tabs.filter((tab) => !tab.permission || canServer(tab.permission))} />

      <Outlet context={context} />
    </>
  );
}

export default function ServerLayout() {
  const { uuid } = useParams();

  if (!uuid) return null;

  // A new connection per server
  return <ServerContent key={uuid} uuid={uuid} />;
}
