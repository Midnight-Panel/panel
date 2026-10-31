import { cn } from "@/utils";
import type { ReactNode } from "react";

/** Card with a section header (customizer style): title, description and optional actions */
export function SectionCard({ title, description, icon, actions, children, className, danger = false }: {
  title: string;
  description?: ReactNode;
  icon?: ReactNode;
  actions?: ReactNode;
  children?: ReactNode;
  className?: string;
  danger?: boolean;
}) {
  return (
    <section
      className={cn(
        "space-y-5 rounded-2xl border bg-white p-5 sm:p-6 dark:bg-white/[0.02]",
        danger ? "border-red-200 dark:border-red-500/30" : "border-gray-200 dark:border-gray-800",
        className,
      )}
    >
      <div className="flex flex-col gap-3 border-b border-gray-100 pb-3 sm:flex-row sm:items-center sm:justify-between dark:border-gray-800/80">
        <div className="flex min-w-0 items-start gap-3">
          {icon && (
            <span
              className={cn(
                "mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg [&>svg]:size-4",
                danger ? "bg-red-50 text-red-500 dark:bg-red-500/10 dark:text-red-400" : "bg-brand-500/10 text-brand-500 dark:text-brand-400",
              )}
            >
              {icon}
            </span>
          )}
          <div className="min-w-0">
            <h3 className={cn("font-outfit text-base font-semibold", danger ? "text-red-600 dark:text-red-500" : "text-gray-900 dark:text-white")}>
              {title}
            </h3>
            {description && <p className="mt-0.5 text-[13px] text-gray-500 dark:text-gray-400">{description}</p>}
          </div>
        </div>
        {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
      </div>
      {children}
    </section>
  );
}

/** Label / value rows inside a card */
export function DefinitionList({ rows }: { rows: Array<[string, ReactNode]> }) {
  return (
    <dl className="divide-y divide-gray-100 dark:divide-white/5">
      {rows.map(([label, value]) => (
        <div key={label} className="flex items-center justify-between gap-4 py-3 first:pt-0 last:pb-0">
          <dt className="text-sm text-gray-500 dark:text-gray-400">{label}</dt>
          <dd className="min-w-0 truncate text-end text-sm font-medium text-gray-800 dark:text-white/90">{value}</dd>
        </div>
      ))}
    </dl>
  );
}
