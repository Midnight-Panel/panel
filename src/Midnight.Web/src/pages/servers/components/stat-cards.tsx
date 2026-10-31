import type { ServerLimits } from "@/api/types";
import { useT } from "@/i18n";
import { ArrowLeftRightIcon, ChipIcon, ClockIcon, DatabaseIcon, LayerIcon } from "@/icons";
import { formatBytes } from "@/lib/format";
import { formatMib, formatUptime } from "@/lib/servers";
import type { ReactNode } from "react";
import type { ServerLive } from "../hooks";
import { Sparkline } from "./sparkline";

function StatCard({ icon, title, value, detail, chart }: { icon: ReactNode; title: string; value: ReactNode; detail?: ReactNode; chart?: ReactNode }) {
  return (
    <div className="flex flex-col overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02]">
      <div className="flex items-start justify-between gap-3 p-4 pb-2">
        <div className="min-w-0">
          <span className="text-xs font-medium text-gray-500 dark:text-gray-400">{title}</span>
          <div className="mt-1 truncate font-outfit text-lg font-semibold text-gray-800 dark:text-white/90">{value}</div>
          {detail && <div className="truncate text-xs text-gray-500 dark:text-gray-400">{detail}</div>}
        </div>
        <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300 [&>svg]:size-4.5">
          {icon}
        </span>
      </div>
      <div className="mt-auto h-12">{chart}</div>
    </div>
  );
}

function rate(bytes: number) {
  return `${formatBytes(Math.max(0, bytes))}/s`;
}

/** CPU, memory, disk, network and uptime from the stats events, with sparklines of the last samples */
export function StatCards({ live, limits }: { live: ServerLive; limits: ServerLimits }) {
  const { t } = useT();
  const stats = live.stats;
  const samples = live.samples;
  const running = live.state !== undefined && live.state !== "Offline" && live.state !== "Installing";

  const rx: number[] = [];
  const tx: number[] = [];

  for (let index = 1; index < samples.length; index++) {
    const previous = samples[index - 1]!;
    const current = samples[index]!;
    const seconds = Math.max(0.5, (current.time - previous.time) / 1000);
    rx.push(Math.max(0, (current.stats.networkRxBytes - previous.stats.networkRxBytes) / seconds));
    tx.push(Math.max(0, (current.stats.networkTxBytes - previous.stats.networkTxBytes) / seconds));
  }

  const memoryLimit = stats?.memoryLimitBytes || limits.memory * 1024 * 1024;

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5">
      <StatCard
        icon={<ChipIcon />}
        title={t("Servers.Cpu")}
        value={stats && running ? `${stats.cpuPercent.toFixed(1)} %` : t("ServerState.Offline")}
        detail={limits.cpu > 0 ? t("Servers.OfLimit", `${limits.cpu} %`) : t("Servers.Unlimited")}
        chart={<Sparkline series={[{ name: "CPU", data: samples.map((sample) => Number(sample.stats.cpuPercent.toFixed(1))) }]} max={limits.cpu > 0 ? limits.cpu : undefined} />}
      />
      <StatCard
        icon={<LayerIcon />}
        title={t("Servers.Memory")}
        value={stats && running ? formatBytes(stats.memoryBytes) : "-"}
        detail={t("Servers.OfLimit", formatMib(limits.memory))}
        chart={<Sparkline series={[{ name: "RAM", data: samples.map((sample) => sample.stats.memoryBytes) }]} max={memoryLimit} />}
      />
      <StatCard
        icon={<DatabaseIcon />}
        title={t("Servers.Disk")}
        value={stats ? formatBytes(stats.diskBytes) : "-"}
        detail={limits.disk > 0 ? t("Servers.OfLimit", formatMib(limits.disk)) : t("Servers.Unlimited")}
        chart={<Sparkline series={[{ name: "Disk", data: samples.map((sample) => sample.stats.diskBytes) }]} max={limits.disk > 0 ? limits.disk * 1024 * 1024 : undefined} />}
      />
      <StatCard
        icon={<ArrowLeftRightIcon />}
        title={t("Servers.Network")}
        value={<span className="text-base">↓ {rate(rx.at(-1) ?? 0)}</span>}
        detail={`↑ ${rate(tx.at(-1) ?? 0)}`}
        chart={<Sparkline series={[{ name: "In", data: rx }, { name: "Out", data: tx }]} colors={["#12B76A", "#F79009"]} />}
      />
      <StatCard
        icon={<ClockIcon />}
        title={t("Servers.Uptime")}
        value={formatUptime(running ? (stats?.uptimeSeconds ?? 0) : 0)}
        detail={stats ? t("Servers.NetworkTotal", formatBytes(stats.networkRxBytes), formatBytes(stats.networkTxBytes)) : undefined}
      />
    </div>
  );
}
