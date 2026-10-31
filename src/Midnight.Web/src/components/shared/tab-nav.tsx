import { cn } from "@/utils";
import type { ReactNode } from "react";
import { Link, useLocation } from "react-router";

export interface TabNavItem {
  label: string;
  to: string;
  icon?: ReactNode;
}

/** Route based line tabs. The longest matching path is the active tab. */
export function TabNav({ items }: { items: TabNavItem[] }) {
  const { pathname } = useLocation();

  const active = [...items]
    .sort((a, b) => b.to.length - a.to.length)
    .find((item) => pathname === item.to || pathname.startsWith(item.to + "/"));

  return (
    <div className="mb-6 overflow-x-auto border-b border-gray-200 dark:border-gray-800">
      <nav className="-mb-px flex gap-2">
        {items.map((item) => (
          <Link
            key={item.to}
            to={item.to}
            className={cn(
              "inline-flex h-12 items-center gap-2 border-b-2 px-3 text-sm font-medium whitespace-nowrap transition-colors [&>svg]:size-5",
              active?.to === item.to
                ? "border-brand-500 text-brand-500 dark:border-brand-400 dark:text-brand-400"
                : "border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200",
            )}
          >
            {item.icon}
            {item.label}
          </Link>
        ))}
      </nav>
    </div>
  );
}
