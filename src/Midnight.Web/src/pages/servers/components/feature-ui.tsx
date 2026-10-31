import { EmptyState } from "@/components/shared/empty-state";
import { Spinner } from "@/components/ui/spinner";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { useT } from "@/i18n";
import { LockIcon } from "@/icons";
import { formatRelative } from "@/lib/relative-time";
import { cn } from "@/utils";
import type { ReactNode } from "react";
import { useServerContext } from "../hooks";

/** Shows the tab only with the server permission (direct links to a hidden tab) */
export function ServerPermissionGate({ permission, children }: { permission: string; children: ReactNode }) {
  const { can } = useServerContext();
  const { t } = useT();

  if (!can(permission)) return <EmptyState icon={<LockIcon />} title={t("Forbidden.Title")} description={t("ServerFeatures.NoPermission")} />;

  return <>{children}</>;
}

/** Card with title row, used for the lists of the feature tabs (customizer card style) */
export function FeatureCard({ title, description, actions, children, className }: {
  title: string;
  description?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02]", className)}>
      <div className="flex flex-col gap-3 border-b border-gray-100 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6 dark:border-gray-800">
        <div className="min-w-0">
          <h3 className="font-outfit text-base font-semibold text-gray-900 dark:text-white">{title}</h3>
          {description && <p className="mt-0.5 text-[13px] text-gray-500 dark:text-gray-400">{description}</p>}
        </div>
        {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {children}
    </div>
  );
}

export function CardLoader() {
  return (
    <div className="flex justify-center py-12 text-brand-500">
      <Spinner className="size-6" />
    </div>
  );
}

/** "3 of 5" usage pill */
export function UsagePill({ used, limit, label }: { used: number; limit: number; label?: string }) {
  const { t } = useT();
  const full = used >= limit;

  return (
    <span
      className={cn(
        "inline-flex h-7 items-center gap-1.5 rounded-lg border px-2.5 text-xs font-medium",
        full
          ? "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-400"
          : "border-gray-200 bg-gray-50 text-gray-600 dark:border-gray-800 dark:bg-white/[0.03] dark:text-gray-300",
      )}
    >
      {label && <span className="text-gray-500 dark:text-gray-400">{label}</span>}
      <span className="tabular-nums">{t("ServerFeatures.OfLimit", used, limit)}</span>
    </span>
  );
}

/** Relative time with the exact date as tooltip */
export function RelativeTime({ value, fallback = "-" }: { value: string | null | undefined; fallback?: string }) {
  const { formatDate } = useT();

  if (!value) return <span>{fallback}</span>;

  return (
    <Tooltip>
      <TooltipTrigger render={<time dateTime={value} className="cursor-default whitespace-nowrap" />}>{formatRelative(value)}</TooltipTrigger>
      <TooltipContent>{formatDate(value)}</TooltipContent>
    </Tooltip>
  );
}

/** Small label / value block inside a list row */
export function Meta({ label, children, className }: { label: string; children: ReactNode; className?: string }) {
  return (
    <div className={cn("min-w-0", className)}>
      <dt className="text-[11px] font-medium tracking-wide text-gray-400 uppercase dark:text-gray-500">{label}</dt>
      <dd className="mt-0.5 truncate text-sm text-gray-800 dark:text-white/90">{children}</dd>
    </div>
  );
}
