import { serversApi } from "@/api/servers";
import type { ServerResources, ServerSummary } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { EmptyState } from "@/components/shared/empty-state";
import { PageHeader } from "@/components/shared/page-header";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import { BoxIcon, ChipIcon, LayoutGridIcon, ListIcon, SearchIcon } from "@/icons";
import { formatBytes } from "@/lib/format";
import { Permissions } from "@/lib/permissions";
import { formatMib } from "@/lib/servers";
import { cn } from "@/utils";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useSearchParams } from "react-router";
import { CopyButton, ServerStatusBadge, StateDot } from "./components/badges";
import { ServerCard } from "./components/server-card";

type Ownership = "all" | "owned" | "shared";
type StateFilter = "all" | "online" | "offline";
type SortBy = "name" | "state";
type View = "grid" | "list";

const VIEW_KEY = "midnight.servers.view";

const selectClass =
  "flex h-11 min-w-[150px] rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90";

function loadView(): View {
  try {
    return localStorage.getItem(VIEW_KEY) === "list" ? "list" : "grid";
  } catch {
    return "grid";
  }
}

/** Running = not offline; servers of unreachable nodes count as offline */
function isRunning(resources: ServerResources | undefined) {
  return !!resources?.nodeOnline && resources.state !== "Offline" && resources.state !== "Installing";
}

export default function ServersPage() {
  const { can } = useAuth();
  const { t } = useT();
  const [params, setParams] = useSearchParams();
  const canSeeAll = can(Permissions.AdminServersRead);
  const all = canSeeAll && params.get("all") === "true";

  const [search, setSearch] = useState("");
  const [ownership, setOwnership] = useState<Ownership>("all");
  const [stateFilter, setStateFilter] = useState<StateFilter>("all");
  const [sortBy, setSortBy] = useState<SortBy>("name");
  const [view, setView] = useState<View>(loadView);

  const servers = useQuery({ queryKey: ["servers", "list", all], queryFn: () => serversApi.list(all) });
  const resources = useQuery({
    queryKey: ["servers", "resources", all],
    queryFn: () => serversApi.allResources(all),
    enabled: (servers.data?.length ?? 0) > 0,
    refetchInterval: 10_000,
  });

  const changeView = (next: View) => {
    setView(next);
    try {
      localStorage.setItem(VIEW_KEY, next);
    } catch {
      // only a convenience
    }
  };

  const list = servers.data ?? [];
  const live = resources.data ?? {};
  const term = search.trim().toLowerCase();

  const visible = list
    .filter((server) => ownership === "all" || (ownership === "owned" ? server.isOwner : !server.isOwner))
    .filter((server) => stateFilter === "all" || isRunning(live[server.uuid]) === (stateFilter === "online"))
    .filter(
      (server) =>
        !term ||
        [server.name, server.description, server.shardName, server.nodeName, server.primaryAllocation]
          .some((value) => value?.toLowerCase().includes(term)),
    )
    .sort((a, b) =>
      sortBy === "state"
        ? Number(isRunning(live[b.uuid])) - Number(isRunning(live[a.uuid])) || a.name.localeCompare(b.name)
        : a.name.localeCompare(b.name),
    );

  const sharedCount = list.filter((server) => !server.isOwner).length;

  return (
    <>
      <PageHeader
        icon={<ChipIcon />}
        title={t("Nav.Servers")}
        description={t("ServerList.Description")}
        actions={
          canSeeAll && (
            <label className="flex cursor-pointer items-center gap-3">
              <Switch
                checked={all}
                onCheckedChange={(checked) => setParams(checked ? { all: "true" } : {}, { replace: true })}
              />
              <span className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("Dashboard.ShowAllServers")}</span>
            </label>
          )
        }
      />

      <div className="mb-6 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-1 flex-wrap items-center gap-3">
          <div className="relative min-w-[220px] flex-1 sm:max-w-xs">
            <SearchIcon className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-gray-400" />
            <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("ServerList.Search")} className="pl-9" />
          </div>

          {sharedCount > 0 && (
            <select value={ownership} onChange={(e) => setOwnership(e.target.value as Ownership)} className={selectClass} aria-label={t("ServerList.Ownership")}>
              <option value="all">{t("ServerList.AllServers")}</option>
              <option value="owned">{t("ServerList.Owned")}</option>
              <option value="shared">{t("ServerList.Shared")}</option>
            </select>
          )}

          <select value={stateFilter} onChange={(e) => setStateFilter(e.target.value as StateFilter)} className={selectClass} aria-label={t("ServerList.State")}>
            <option value="all">{t("ServerList.AnyState")}</option>
            <option value="online">{t("ServerList.Running")}</option>
            <option value="offline">{t("ServerList.Stopped")}</option>
          </select>

          <select value={sortBy} onChange={(e) => setSortBy(e.target.value as SortBy)} className={selectClass} aria-label={t("ServerList.Sort")}>
            <option value="name">{t("ServerList.SortName")}</option>
            <option value="state">{t("ServerList.SortState")}</option>
          </select>
        </div>

        <div className="inline-flex shrink-0 rounded-[10px] border border-gray-200 bg-gray-50 p-1 dark:border-gray-800 dark:bg-white/[0.02]" role="group" aria-label={t("ServerList.View")}>
          {(["grid", "list"] as const).map((option) => (
            <button
              key={option}
              type="button"
              onClick={() => changeView(option)}
              aria-pressed={view === option}
              title={t(option === "grid" ? "ServerList.Grid" : "ServerList.List")}
              className={cn(
                "flex size-9 items-center justify-center rounded-lg transition-colors [&>svg]:size-4",
                view === option
                  ? "bg-white text-gray-900 shadow-theme-xs dark:bg-white/10 dark:text-white"
                  : "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white",
              )}
            >
              {option === "grid" ? <LayoutGridIcon /> : <ListIcon />}
            </button>
          ))}
        </div>
      </div>

      {servers.isPending ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 md:gap-6 2xl:grid-cols-3">
          {[0, 1, 2].map((index) => <Skeleton key={index} className="h-64 rounded-2xl" />)}
        </div>
      ) : list.length === 0 ? (
        <EmptyState icon={<BoxIcon />} title={t("Dashboard.NoServers")} description={t("Dashboard.NoServersHint")} />
      ) : visible.length === 0 ? (
        <EmptyState icon={<SearchIcon />} title={t("ServerList.NoMatches")} description={t("ServerList.NoMatchesHint")} />
      ) : view === "grid" ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 md:gap-6 2xl:grid-cols-3">
          {visible.map((server) => <ServerCard key={server.uuid} server={server} resources={live[server.uuid]} />)}
        </div>
      ) : (
        <ServerTable servers={visible} live={live} />
      )}
    </>
  );
}

function ServerTable({ servers, live }: { servers: ServerSummary[]; live: Record<string, ServerResources> }) {
  const { t } = useT();

  return (
    <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02]">
      <div className="overflow-x-auto">
        <table className="w-full min-w-[720px] text-sm">
          <thead>
            <tr className="border-b border-gray-100 text-left text-xs font-medium text-gray-500 dark:border-gray-800 dark:text-gray-400">
              <th className="px-5 py-3">{t("ServerList.Name")}</th>
              <th className="px-5 py-3">{t("ServerList.Address")}</th>
              <th className="px-5 py-3">{t("Servers.Cpu")}</th>
              <th className="px-5 py-3">{t("Servers.Memory")}</th>
              <th className="px-5 py-3">{t("Servers.Disk")}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
            {servers.map((server) => {
              const resources = live[server.uuid];
              const state = resources?.nodeOnline === false ? undefined : resources?.state;
              const running = isRunning(resources);
              const stats = resources?.stats;

              return (
                <tr key={server.uuid} className="transition-colors hover:bg-gray-50 dark:hover:bg-white/[0.02]">
                  <td className="px-5 py-3.5">
                    <Link to={`/servers/${server.uuid}`} className="group flex items-center gap-3">
                      <StateDot state={state} />
                      <span className="min-w-0">
                        <span className="flex items-center gap-2 font-semibold text-gray-800 group-hover:text-brand-500 dark:text-white/90 dark:group-hover:text-brand-400">
                          <span className="truncate">{server.name}</span>
                          {!server.isOwner && <Badge size="sm" color="gray">{t("ServerList.SharedBadge")}</Badge>}
                          {(server.suspended || server.status !== "Ready") && <ServerStatusBadge status={server.status} suspended={server.suspended} />}
                        </span>
                        <span className="block truncate text-xs text-gray-500 dark:text-gray-400">{server.shardName} · {server.nodeName}</span>
                      </span>
                    </Link>
                  </td>
                  <td className="px-5 py-3.5">
                    {server.primaryAllocation ? (
                      <span className="flex items-center gap-1 font-mono text-xs text-gray-700 dark:text-gray-300">
                        {server.primaryAllocation}
                        <CopyButton text={server.primaryAllocation} />
                      </span>
                    ) : (
                      <span className="text-gray-400">-</span>
                    )}
                  </td>
                  <td className="px-5 py-3.5 text-gray-700 dark:text-gray-300">
                    {running && stats ? `${stats.cpuPercent.toFixed(1)} %` : <span className="text-gray-400">{t(`ServerState.${state ?? "Offline"}`)}</span>}
                  </td>
                  <td className="px-5 py-3.5 text-gray-700 dark:text-gray-300">
                    {running && stats ? formatBytes(stats.memoryBytes) : "0 B"}
                    <span className="text-gray-400"> / {formatMib(server.limits.memory)}</span>
                  </td>
                  <td className="px-5 py-3.5 text-gray-700 dark:text-gray-300">
                    {stats ? formatBytes(stats.diskBytes) : "-"}
                    <span className="text-gray-400"> / {server.limits.disk > 0 ? formatMib(server.limits.disk) : "∞"}</span>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
