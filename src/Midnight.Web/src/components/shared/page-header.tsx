import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from "@/components/ui/breadcrumb";
import type { ReactNode } from "react";

/** Page title with icon, description and actions (same header style as the customizer) */
export function PageHeader({ title, description, actions, icon, crumbs = [] }: {
  title: string;
  description?: ReactNode;
  actions?: ReactNode;
  icon?: ReactNode;
  /** Parent pages shown as breadcrumb above the title, e.g. Nodes on a node detail page */
  crumbs?: Array<{ label: string; to: string }>;
}) {
  return (
    <div className="mb-6 flex flex-col gap-4 border-b border-gray-200 pb-4 sm:flex-row sm:items-center sm:justify-between dark:border-gray-800">
      <div className="min-w-0">
        {crumbs.length > 0 && (
          <Breadcrumb className="mb-2">
            <BreadcrumbList>
              {crumbs.map((crumb) => (
                <span key={crumb.to} className="contents">
                  <BreadcrumbItem>
                    <BreadcrumbLink to={crumb.to}>{crumb.label}</BreadcrumbLink>
                  </BreadcrumbItem>
                  <BreadcrumbSeparator />
                </span>
              ))}
              <BreadcrumbItem>
                <BreadcrumbPage>{title}</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        )}

        <div className="flex items-center gap-2.5">
          {icon && (
            <div className="shrink-0 rounded-xl bg-brand-500/10 p-2 text-brand-500 dark:text-brand-400 [&>svg]:size-5">
              {icon}
            </div>
          )}
          <h1 className="truncate font-outfit text-xl font-bold text-gray-900 sm:text-2xl dark:text-white">{title}</h1>
        </div>
        {description && <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{description}</p>}
      </div>

      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}
