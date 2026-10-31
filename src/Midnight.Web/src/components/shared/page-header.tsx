import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from "@/components/ui/breadcrumb";
import { useT } from "@/i18n";
import type { ReactNode } from "react";

/** Page title with description and breadcrumb (TailAdmin page header style) */
export function PageHeader({ title, description, actions, crumbs = [] }: {
  title: string;
  description?: string;
  actions?: ReactNode;
  /** Intermediate breadcrumb entries between "Dashboard" and the current page */
  crumbs?: Array<{ label: string; to: string }>;
}) {
  const { t } = useT();

  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 className="text-title-lg font-semibold text-gray-800 dark:text-white/90">{title}</h2>
        {description && <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{description}</p>}
      </div>

      <div className="flex flex-wrap items-center gap-3">
        {actions}
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink to="/">{t("Nav.Dashboard")}</BreadcrumbLink>
            </BreadcrumbItem>
            {crumbs.map((crumb) => (
              <span key={crumb.to} className="contents">
                <BreadcrumbSeparator />
                <BreadcrumbItem>
                  <BreadcrumbLink to={crumb.to}>{crumb.label}</BreadcrumbLink>
                </BreadcrumbItem>
              </span>
            ))}
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbPage>{title}</BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>
      </div>
    </div>
  );
}
