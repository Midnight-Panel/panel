import { cn } from "@/utils";
import type { ReactNode } from "react";

/** Key figure card, same look as the TailAdmin dashboard metrics */
export function MetricCard({ icon, title, value, footer, className }: {
  icon: ReactNode;
  title: string;
  value: ReactNode;
  footer?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("rounded-2xl border border-gray-200 bg-white p-5 md:p-6 dark:border-gray-800 dark:bg-white/3", className)}>
      <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-white/90 [&>svg]:size-6">
        {icon}
      </div>
      <div className="mt-5 flex items-end justify-between">
        <div>
          <span className="text-sm text-gray-500 dark:text-gray-400">{title}</span>
          <h4 className="mt-2 text-title-sm font-bold text-gray-800 dark:text-white/90">{value}</h4>
        </div>
        {footer}
      </div>
    </div>
  );
}
