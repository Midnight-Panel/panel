import { cn } from "@/utils";

/** Thin usage bar, amber from 70 %, red from 90 % (same as the node overview) */
export function UsageBar({ label, value, detail, className }: { label: string; value: number | null; detail: string; className?: string }) {
  const percent = value === null ? 0 : Math.max(0, Math.min(100, value));

  return (
    <div className={cn("space-y-1.5", className)}>
      <div className="flex items-center justify-between gap-2 text-xs">
        <span className="text-gray-500 dark:text-gray-400">{label}</span>
        <span className="truncate font-medium text-gray-700 dark:text-gray-300">{detail}</span>
      </div>
      <div className="h-1.5 w-full overflow-hidden rounded-xs bg-gray-200 dark:bg-gray-800">
        <div
          className={cn("h-full rounded-xs transition-all duration-500", percent >= 90 ? "bg-red-500" : percent >= 70 ? "bg-amber-500" : "bg-brand-500")}
          style={{ width: `${percent}%` }}
        />
      </div>
    </div>
  );
}
