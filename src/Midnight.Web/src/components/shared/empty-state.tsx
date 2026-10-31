import { cn } from "@/utils";
import type { ReactNode } from "react";

export function EmptyState({ icon, title, description, children, className }: {
  icon: ReactNode;
  title: string;
  description?: string;
  children?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("flex flex-col items-center justify-center rounded-2xl border border-gray-200 bg-white px-6 py-14 text-center dark:border-gray-800 dark:bg-white/3", className)}>
      <div className="mb-5 flex size-16 items-center justify-center rounded-full bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400 [&>svg]:size-8">
        {icon}
      </div>
      <h3 className="text-lg font-semibold text-gray-800 dark:text-white/90">{title}</h3>
      {description && <p className="mt-2 max-w-md text-sm text-gray-500 dark:text-gray-400">{description}</p>}
      {children}
    </div>
  );
}
