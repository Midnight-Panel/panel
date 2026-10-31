import { EmptyState } from "@/components/shared/empty-state";
import { PageLoader } from "@/components/shared/guards";
import { MetricCard } from "@/components/shared/metric-card";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import type { ReactNode } from "react";
import { BoxIcon, ChipIcon, DatabaseIcon, LayerIcon, OctagonAlertIcon } from "@/icons";
import { formatBytes, formatTimeSpan, percent } from "@/lib/format";
import { cn } from "@/utils";
import { Link } from "react-router";
import { useNode } from "./layout";
import { useNodeStatus } from "./status-badge";

function Usage({ value }: { value: number }) {
  return (
    <div className="mt-4 h-1.5 w-full overflow-hidden rounded-xs bg-gray-200 dark:bg-gray-800">
      <div
        className={cn("h-full rounded-xs", value >= 90 ? "bg-red-500" : value >= 70 ? "bg-amber-500" : "bg-brand-500")}
        style={{ width: `${Math.min(100, value)}%` }}
      />
    </div>
  );
}

export default function NodeOverviewPage() {
  const node = useNode();
  const { t } = useT();
  const { data, isFetching } = useNodeStatus(node.id, 5_000);

  if (!data) return <PageLoader />;

  if (!data.online || !data.system) {
    return (
      <div className="space-y-6">
        <Alert variant="destructive" className="px-4 py-3">
          <OctagonAlertIcon />
          <AlertTitle>{t("Nodes.Unreachable")}</AlertTitle>
          <AlertDescription>{data.error}</AlertDescription>
        </Alert>
        <EmptyState icon={<LayerIcon />} title={t("Nodes.NotConnected")} description={t("Nodes.NotConnectedHint")}>
          <Button className="mt-6" render={<Link to={`/admin/nodes/${node.id}/setup`} />} nativeButton={false}>{t("Nodes.Setup")}</Button>
        </EmptyState>
      </div>
    );
  }

  const system = data.system;
  const memory = percent(system.memoryUsedBytes, system.memoryTotalBytes);
  const disk = percent(system.diskUsedBytes, system.diskTotalBytes);

  const rows: Array<[string, ReactNode]> = [
    [t("Nodes.Hostname"), system.hostname],
    [t("AdminOverview.OperatingSystem"), system.operatingSystem],
    [t("Nodes.Kernel"), `${system.kernelVersion} (${system.architecture})`],
    [t("Nodes.Cpu"), `${system.cpuModel ?? "-"} · ${system.cpuCores} ${t("Nodes.Cores")}`],
    [t("AdminOverview.Uptime"), formatTimeSpan(system.uptime)],
    [t("Nodes.DemonVersion"), `v${system.demonVersion} · ${t("Nodes.Protocol")} ${system.protocolVersion}`],
    [t("Nodes.Latency"), `${data.latencyMs} ms`],
  ];

  return (
    <div className={cn("space-y-6 transition-opacity", isFetching && "opacity-95")}>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:gap-6 xl:grid-cols-4">
        <MetricCard icon={<ChipIcon />} title={t("Dashboard.Cpu")} value={`${system.cpuUsage.toFixed(1)} %`} />
        <MetricCard icon={<LayerIcon />} title={t("Dashboard.Memory")} value={`${formatBytes(system.memoryUsedBytes)} / ${formatBytes(system.memoryTotalBytes)}`} />
        <MetricCard icon={<DatabaseIcon />} title={t("Dashboard.Disk")} value={`${formatBytes(system.diskUsedBytes)} / ${formatBytes(system.diskTotalBytes)}`} />
        <MetricCard
          icon={<BoxIcon />}
          title="Docker"
          value={system.docker.available ? `v${system.docker.version}` : t("Nodes.Offline")}
          footer={system.docker.available
            ? <Badge size="sm" color="success">{system.docker.containersRunning}/{system.docker.containers} {t("Nodes.Running")}</Badge>
            : <Badge size="sm" color="error">{t("Nodes.Offline")}</Badge>}
        />
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3 md:gap-6">
        {[
          [t("Dashboard.Cpu"), system.cpuUsage],
          [t("Dashboard.Memory"), memory],
          [t("Dashboard.Disk"), disk],
        ].map(([label, value]) => (
          <div key={label as string} className="rounded-2xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-white/[0.02]">
            <div className="flex items-center justify-between text-sm">
              <span className="text-gray-500 dark:text-gray-400">{label}</span>
              <span className="font-semibold text-gray-800 dark:text-white/90">{Math.round(value as number)} %</span>
            </div>
            <Usage value={value as number} />
          </div>
        ))}
      </div>

      {!system.docker.available && (
        <Alert variant="warning" className="px-4 py-3">
          <OctagonAlertIcon />
          <AlertTitle>{t("Nodes.DockerUnavailable")}</AlertTitle>
          <AlertDescription>{system.docker.error}</AlertDescription>
        </Alert>
      )}

      <div className="rounded-2xl border border-gray-200 bg-white p-5 lg:p-6 dark:border-gray-800 dark:bg-white/[0.02]">
        <h3 className="mb-4 text-lg font-semibold text-gray-800 dark:text-white/90">{t("AdminOverview.System")}</h3>
        <dl className="divide-y divide-gray-100 dark:divide-white/5">
          {rows.map(([label, value]) => (
            <div key={label} className="flex items-center justify-between gap-4 py-3">
              <dt className="text-sm text-gray-500 dark:text-gray-400">{label}</dt>
              <dd className="text-end text-sm font-medium text-gray-800 dark:text-white/90">{value}</dd>
            </div>
          ))}
        </dl>
      </div>
    </div>
  );
}
