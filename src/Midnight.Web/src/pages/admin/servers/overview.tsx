import { serversApi } from "@/api/servers";
import { DefinitionList, SectionCard } from "@/components/shared/section-card";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { useT } from "@/i18n";
import { ChipIcon, GlobeIcon, InfoCircleIcon, LayerIcon, OctagonAlertIcon } from "@/icons";
import { formatBytes } from "@/lib/format";
import { allocationLabel, formatCpu, formatMib, formatUptime } from "@/lib/servers";
import { CopyButton, ServerStateBadge } from "@/pages/servers/components/badges";
import { UsageBar } from "@/pages/servers/components/usage";
import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { Link } from "react-router";
import { useAdminServer } from "./hooks";

export default function AdminServerOverviewPage() {
  const server = useAdminServer();
  const { t, formatDate } = useT();

  const { data: resources } = useQuery({
    queryKey: ["servers", server.uuid, "resources"],
    queryFn: () => serversApi.resources(server.uuid),
    refetchInterval: 10_000,
  });

  const stats = resources?.stats;
  const memoryLimit = server.limits.memory * 1024 * 1024;
  const diskLimit = server.limits.disk * 1024 * 1024;

  const info: Array<[string, ReactNode]> = [
    ["UUID", <span key="uuid" className="inline-flex items-center gap-1 font-mono text-xs">{server.uuid}<CopyButton text={server.uuid} /></span>],
    [t("Servers.Owner"), <Link key="owner" to={`/admin/users/${server.ownerId}`} className="hover:text-brand-500">{server.ownerUsername} · {server.ownerEmail}</Link>],
    [t("Servers.Node"), <Link key="node" to={`/admin/nodes/${server.nodeId}`} className="hover:text-brand-500">{server.nodeName}</Link>],
    [t("Servers.Shard"), <Link key="shard" to={`/admin/shards/${server.shardId}`} className="hover:text-brand-500">{server.shardName}</Link>],
    [t("Servers.DockerImage"), <span key="image" className="font-mono text-xs">{server.dockerImage}</span>],
    [t("Servers.CreatedAt"), formatDate(server.createdAt)],
    [t("Servers.InstalledAt"), formatDate(server.installedAt)],
  ];

  const limits: Array<[string, ReactNode]> = [
    [t("Servers.Cpu"), formatCpu(server.limits.cpu, t)],
    [t("Servers.Memory"), formatMib(server.limits.memory)],
    [t("Servers.Swap"), server.limits.swap === 0 ? t("Servers.Disabled") : server.limits.swap === -1 ? t("Servers.Unlimited") : formatMib(server.limits.swap)],
    [t("Servers.Disk"), server.limits.disk > 0 ? formatMib(server.limits.disk) : t("Servers.Unlimited")],
    [t("Servers.IoWeight"), server.limits.ioWeight],
    [t("Servers.Threads"), server.limits.threads || t("Servers.AllCores")],
    [t("Servers.OomKiller"), server.limits.oomKillerDisabled ? t("Servers.Disabled") : t("Servers.Enabled")],
    [t("Servers.FeatureLimits"), t("Servers.FeatureLimitsSummary", server.featureLimits.databases, server.featureLimits.allocations, server.featureLimits.backups)],
  ];

  return (
    <div className="space-y-6">
      {server.suspended && (
        <Alert variant="warning" className="px-4 py-3">
          <OctagonAlertIcon />
          <AlertTitle>{t("Servers.Status.Suspended")}</AlertTitle>
          <AlertDescription>{t("Servers.SuspendedHint")}</AlertDescription>
        </Alert>
      )}
      {server.status === "InstallFailed" && (
        <Alert variant="destructive" className="px-4 py-3">
          <OctagonAlertIcon />
          <AlertTitle>{t("Servers.Status.InstallFailed")}</AlertTitle>
          <AlertDescription>{t("Servers.InstallFailedHint")}</AlertDescription>
        </Alert>
      )}

      <SectionCard
        icon={<ChipIcon />}
        title={t("Servers.LiveResources")}
        description={t("Servers.LiveResourcesHint")}
        actions={<ServerStateBadge state={resources?.state} nodeOnline={resources?.nodeOnline ?? true} />}
      >
        <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
          <UsageBar
            label={t("Servers.Cpu")}
            value={stats && server.limits.cpu > 0 ? (stats.cpuPercent / server.limits.cpu) * 100 : null}
            detail={stats ? `${stats.cpuPercent.toFixed(1)} % / ${server.limits.cpu > 0 ? `${server.limits.cpu} %` : "∞"}` : "-"}
          />
          <UsageBar
            label={t("Servers.Memory")}
            value={stats ? (stats.memoryBytes / memoryLimit) * 100 : null}
            detail={stats ? `${formatBytes(stats.memoryBytes)} / ${formatMib(server.limits.memory)}` : "-"}
          />
          <UsageBar
            label={t("Servers.Disk")}
            value={stats && diskLimit > 0 ? (stats.diskBytes / diskLimit) * 100 : null}
            detail={stats ? `${formatBytes(stats.diskBytes)} / ${server.limits.disk > 0 ? formatMib(server.limits.disk) : "∞"}` : "-"}
          />
        </div>
        <p className="text-xs text-gray-500 dark:text-gray-400">
          {t("Servers.Uptime")}: <span className="font-medium text-gray-700 dark:text-gray-300">{formatUptime(stats?.uptimeSeconds ?? 0)}</span>
        </p>
      </SectionCard>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <SectionCard icon={<InfoCircleIcon />} title={t("Servers.Information")}>
          <DefinitionList rows={info} />
          {server.description && <p className="border-t border-gray-100 pt-4 text-sm text-gray-500 dark:border-white/5 dark:text-gray-400">{server.description}</p>}
        </SectionCard>

        <div className="space-y-6">
          <SectionCard icon={<LayerIcon />} title={t("Servers.Limits")}>
            <DefinitionList rows={limits} />
          </SectionCard>

          <SectionCard icon={<GlobeIcon />} title={t("Nodes.Allocations")}>
            <ul className="divide-y divide-gray-100 dark:divide-white/5">
              {server.allocations.map((allocation) => (
                <li key={allocation.id} className="flex items-center justify-between gap-3 py-2.5 first:pt-0 last:pb-0">
                  <span className="flex min-w-0 items-center gap-2">
                    <span className="font-mono text-sm text-gray-800 dark:text-white/90">{allocationLabel(allocation)}</span>
                    {allocation.notes && <span className="truncate text-xs text-gray-500 dark:text-gray-400">{allocation.notes}</span>}
                  </span>
                  {allocation.isPrimary && <Badge size="sm" color="primary">{t("Servers.Primary")}</Badge>}
                </li>
              ))}
            </ul>
          </SectionCard>
        </div>
      </div>
    </div>
  );
}
