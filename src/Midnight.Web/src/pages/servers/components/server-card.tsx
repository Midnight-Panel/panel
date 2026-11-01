import type { ServerResources, ServerSummary } from "@/api/types";
import { useT } from "@/i18n";
import { ChipIcon } from "@/icons";
import { formatBytes } from "@/lib/format";
import { formatMib } from "@/lib/servers";
import { Link } from "react-router";
import { CopyButton, ServerStatusBadge, StateDot } from "./badges";
import { UsageBar } from "./usage";

/** Server tile with live state, address and usage bars (dashboard and servers page) */
export function ServerCard({ server, resources }: { server: ServerSummary; resources: ServerResources | undefined }) {
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
