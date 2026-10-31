import { cn } from "@/utils";
import type { ReactNode } from "react";
import { Link, useLocation } from "react-router";

export interface TabNavItem {
  label: string;
  to: string;
  icon?: ReactNode;
}

/** Route based underline tabs (customizer style). The longest matching path is the active tab. */
export function TabNav({ items }: { items: TabNavItem[] }) {
  const { pathname } = useLocation();

  const active = [...items]
    .sort((a, b) => b.to.length - a.to.length)
    .find((item) => pathname === item.to || pathname.startsWith(item.to + "/"));

  return (
    <nav className="scrollbar-none mb-6 flex items-center gap-6 overflow-x-auto border-b border-gray-200 text-sm dark:border-gray-800">
      {items.map((item) => {
        const isActive = active?.to === item.to;

        return (
          <Link
            key={item.to}
            to={item.to}
            aria-current={isActive ? "page" : undefined}
            className={cn(
              "relative flex items-center gap-2 rounded-sm py-3.5 whitespace-nowrap transition-colors select-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:outline-none [&>svg]:size-4",
              isActive
                ? "font-semibold text-gray-900 dark:text-white"
                : "font-medium text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200",
            )}
          >
            {item.icon}
            {item.label}
            {isActive && <span className="absolute inset-x-0 bottom-0 h-0.5 rounded-full bg-brand-500" />}
          </Link>
        );
      })}
    </nav>
  );
}
