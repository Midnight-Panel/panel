import { serversApi } from "@/api/servers";
import type { ServerResources, ServerSummary } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { EmptyState } from "@/components/shared/empty-state";
import { MetricCard } from "@/components/shared/metric-card";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import { BoxIcon, ChipIcon, DatabaseIcon, LayerIcon } from "@/icons";
import { formatBytes } from "@/lib/format";
import { Permissions } from "@/lib/permissions";
import { formatMib } from "@/lib/servers";
import { CopyButton, ServerStatusBadge, StateDot } from "@/pages/servers/components/badges";
import { UsageBar } from "@/pages/servers/components/usage";
import { useQuery } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router";

function greeting(hour: number): [string, string] {
  if (hour >= 23 || hour < 5) return ["😴", "Greeting.Night"];
  if (hour < 10) return ["😄", "Greeting.Morning"];
  if (hour < 14) return ["☀️", "Greeting.Day"];
  if (hour < 16) return ["😃", "Greeting.Afternoon"];
  if (hour < 22) return ["🌥️", "Greeting.Evening"];
  return ["🌙", "Greeting.Sleep"];
}

function ServerCard({ server, resources }: { server: ServerSummary; resources: ServerResources | undefined }) {
  const { t } = useT();
  const stats = resources?.stats;
  const state = resources?.nodeOnline === false ? undefined : resources?.state;
  const running = state !== undefined && state !== "Offline" && state !== "Installing";
  const memoryLimit = server.limits.memory * 1024 * 1024;
  const diskLimit = server.limits.disk * 1024 * 1024;
  const showStatus = server.suspended || server.status !== "Ready";

  return (
    <Link
      to={`/servers/${server.uuid}`}
      className="group flex flex-col rounded-2xl border border-gray-200 bg-white p-5 transition-colors hover:border-gray-300 dark:border-gray-800 dark:bg-white/[0.02] dark:hover:border-gray-700"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300 [&>svg]:size-5">
            <ChipIcon />
          </span>
          <div className="min-w-0">
            <h3 className="flex items-center gap-2 font-semibold text-gray-800 group-hover:text-brand-500 dark:text-white/90 dark:group-hover:text-brand-400">
              <StateDot state={state} />
              <span className="truncate">{server.name}</span>
            </h3>
            <p className="truncate text-xs text-gray-500 dark:text-gray-400">{server.shardName} · {server.nodeName}</p>
          </div>
        </div>
        {showStatus ? (
          <ServerStatusBadge status={server.status} suspended={server.suspended} />
        ) : (
          resources && !resources.nodeOnline && <span className="text-xs text-gray-400">{t("Servers.NodeUnreachable")}</span>
        )}
      </div>

      {server.primaryAllocation && (
        <div className="mt-4 flex items-center justify-between rounded-lg bg-gray-50 py-1 ps-3 pe-1 dark:bg-white/[0.03]">
          <span className="font-mono text-xs text-gray-700 dark:text-gray-300">{server.primaryAllocation}</span>
          <CopyButton text={server.primaryAllocation} />
        </div>
      )}

      <div className="mt-4 space-y-3">
        <UsageBar
          label={t("Servers.Cpu")}
          value={running && stats && server.limits.cpu > 0 ? (stats.cpuPercent / server.limits.cpu) * 100 : running ? null : 0}
          detail={running && stats ? `${stats.cpuPercent.toFixed(1)} % / ${server.limits.cpu > 0 ? `${server.limits.cpu} %` : "∞"}` : t(`ServerState.${state ?? "Offline"}`)}
        />
        <UsageBar
          label={t("Servers.Memory")}
          value={running && stats ? (stats.memoryBytes / memoryLimit) * 100 : 0}
          detail={`${running && stats ? formatBytes(stats.memoryBytes) : "0 B"} / ${formatMib(server.limits.memory)}`}
        />
        <UsageBar
          label={t("Servers.Disk")}
          value={stats && diskLimit > 0 ? (stats.diskBytes / diskLimit) * 100 : 0}
          detail={`${stats ? formatBytes(stats.diskBytes) : "-"} / ${server.limits.disk > 0 ? formatMib(server.limits.disk) : "∞"}`}
        />
      </div>
    </Link>
  );
}

export default function DashboardPage() {
  const { user, can } = useAuth();
  const { t } = useT();
  const [emoji, key] = greeting(new Date().getHours());
  const [params, setParams] = useSearchParams();
  const canSeeAll = can(Permissions.AdminServersRead);
  const all = canSeeAll && params.get("all") === "true";

  const servers = useQuery({ queryKey: ["servers", "list", all], queryFn: () => serversApi.list(all) });
  const resources = useQuery({
    queryKey: ["servers", "resources", all],
    queryFn: () => serversApi.allResources(all),
    enabled: (servers.data?.length ?? 0) > 0,
    refetchInterval: 10_000,
  });

  const list = servers.data ?? [];
  const live = Object.values(resources.data ?? {});
  const online = live.filter((entry) => entry.nodeOnline && entry.state === "Online").length;
  const cpu = live.reduce((sum, entry) => sum + (entry.state !== "Offline" ? entry.stats.cpuPercent : 0), 0);
  const memory = live.reduce((sum, entry) => sum + (entry.state !== "Offline" ? entry.stats.memoryBytes : 0), 0);
  const disk = live.reduce((sum, entry) => sum + entry.stats.diskBytes, 0);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 rounded-2xl border border-gray-200 bg-white p-5 sm:flex-row sm:items-center sm:justify-between md:p-6 dark:border-gray-800 dark:bg-white/[0.02]">
        <div>
          <h2 className="text-title-sm font-semibold text-gray-800 dark:text-white/90">
            {emoji} {t(key)}
            <span className="text-brand-500 dark:text-brand-400">{user?.username}</span>
          </h2>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{t("Dashboard.Subtitle")}</p>
        </div>
        {canSeeAll && (
          <label className="flex cursor-pointer items-center gap-3">
            <Switch
              checked={all}
              onCheckedChange={(checked) => setParams(checked ? { all: "true" } : {}, { replace: true })}
            />
            <span className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("Dashboard.ShowAllServers")}</span>
          </label>
        )}
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:gap-6 xl:grid-cols-4">
        <MetricCard icon={<BoxIcon />} title={t("Dashboard.Servers")} value={list.length} footer={list.length > 0 && <span className="text-xs text-gray-500 dark:text-gray-400">{t("Dashboard.OnlineCount", online)}</span>} />
        <MetricCard icon={<ChipIcon />} title={t("Dashboard.Cpu")} value={`${cpu.toFixed(1)} %`} />
        <MetricCard icon={<LayerIcon />} title={t("Dashboard.Memory")} value={formatBytes(memory)} />
        <MetricCard icon={<DatabaseIcon />} title={t("Dashboard.Disk")} value={formatBytes(disk)} />
      </div>

      {servers.isPending ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 md:gap-6 2xl:grid-cols-3">
          {[0, 1, 2].map((index) => <Skeleton key={index} className="h-64 rounded-2xl" />)}
        </div>
      ) : list.length === 0 ? (
        <EmptyState icon={<BoxIcon />} title={t("Dashboard.NoServers")} description={t("Dashboard.NoServersHint")} />
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 md:gap-6 2xl:grid-cols-3">
          {list.map((server) => <ServerCard key={server.uuid} server={server} resources={resources.data?.[server.uuid]} />)}
        </div>
      )}
    </div>
  );
}
