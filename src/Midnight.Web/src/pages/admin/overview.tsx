import { api } from "@/api/client";
import type { AdminOverview } from "@/api/types";
import { EmptyState } from "@/components/shared/empty-state";
import { PageLoader, RequirePermission } from "@/components/shared/guards";
import { MetricCard } from "@/components/shared/metric-card";
import { PageHeader } from "@/components/shared/page-header";
import { useT } from "@/i18n";
import { BoxIcon, GroupIcon, LayerIcon, LockIcon, PieChartIcon } from "@/icons";
import { Permissions } from "@/lib/permissions";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";
import { formatTimeSpan as formatUptime } from "@/lib/format";

function OverviewContent() {
  const { t } = useT();
  const { data } = useQuery({ queryKey: ["admin", "overview"], queryFn: () => api.get<AdminOverview>("/api/admin/overview"), refetchInterval: 10_000 });

  if (!data) return <PageLoader />;

  const rows: Array<[string, string]> = [
    [t("AdminOverview.Version"), data.version],
    [t("AdminOverview.Uptime"), formatUptime(data.uptime)],
    [t("AdminOverview.Memory"), `${Math.round(data.memoryUsageBytes / 1024 / 1024)} MB`],
    [t("AdminOverview.OperatingSystem"), data.operatingSystem],
    [t("AdminOverview.Runtime"), data.runtime],
  ];

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:gap-6 xl:grid-cols-4">
        <Link to="/admin/users"><MetricCard icon={<GroupIcon />} title={t("AdminOverview.Users")} value={data.userCount} className="transition hover:border-brand-300 dark:hover:border-brand-500/40" /></Link>
        <MetricCard icon={<BoxIcon />} title={t("AdminOverview.Servers")} value={data.serverCount} />
        <MetricCard icon={<LayerIcon />} title={t("AdminOverview.Nodes")} value={data.nodeCount} />
        <MetricCard icon={<LockIcon />} title={t("AdminOverview.LockedUsers")} value={data.lockedUserCount} />
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
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
        {/* TODO: Phase 2 - node status list */}
        <EmptyState icon={<LayerIcon />} title={t("AdminOverview.NodeStatus")} description={t("AdminOverview.NoNodes")} />
      </div>
    </div>
  );
}

export default function AdminOverviewPage() {
  const { t } = useT();

  return (
    <RequirePermission permission={Permissions.AdminOverview}>
      <PageHeader icon={<PieChartIcon />} title={t("Nav.Admin.Overview")} description={t("AdminOverview.Description")} />
      <OverviewContent />
    </RequirePermission>
  );
}
