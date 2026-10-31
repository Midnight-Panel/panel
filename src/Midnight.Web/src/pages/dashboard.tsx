import { useAuth } from "@/auth/AuthContext";
import { EmptyState } from "@/components/shared/empty-state";
import { MetricCard } from "@/components/shared/metric-card";
import { useT } from "@/i18n";
import { BoxIcon, ChipIcon, DatabaseIcon, LayerIcon } from "@/icons";

function greeting(hour: number): [string, string] {
  if (hour >= 23 || hour < 5) return ["😴", "Greeting.Night"];
  if (hour < 10) return ["😄", "Greeting.Morning"];
  if (hour < 14) return ["☀️", "Greeting.Day"];
  if (hour < 16) return ["😃", "Greeting.Afternoon"];
  if (hour < 22) return ["🌥️", "Greeting.Evening"];
  return ["🌙", "Greeting.Sleep"];
}

export default function DashboardPage() {
  const { user } = useAuth();
  const { t } = useT();
  const [emoji, key] = greeting(new Date().getHours());

  return (
    <div className="space-y-6">
      <div className="rounded-2xl border border-gray-200 bg-white p-5 md:p-6 dark:border-gray-800 dark:bg-white/[0.02]">
        <h2 className="text-title-sm font-semibold text-gray-800 dark:text-white/90">
          {emoji} {t(key)}
          <span className="text-brand-500 dark:text-brand-400">{user?.username}</span>
        </h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{t("Dashboard.Subtitle")}</p>
      </div>

      {/* TODO: Phase 3 - real values and the server list of the user */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:gap-6 xl:grid-cols-4">
        <MetricCard icon={<BoxIcon />} title={t("Dashboard.Servers")} value="0" />
        <MetricCard icon={<ChipIcon />} title={t("Dashboard.Cpu")} value="0 %" />
        <MetricCard icon={<LayerIcon />} title={t("Dashboard.Memory")} value="0 MB" />
        <MetricCard icon={<DatabaseIcon />} title={t("Dashboard.Disk")} value="0 MB" />
      </div>

      <EmptyState icon={<BoxIcon />} title={t("Dashboard.NoServers")} description={t("Dashboard.NoServersHint")} />
    </div>
  );
}
