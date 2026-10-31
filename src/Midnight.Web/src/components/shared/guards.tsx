import { useAuth } from "@/auth/AuthContext";
import { Spinner } from "@/components/ui/spinner";
import { useT } from "@/i18n";
import { LockIcon } from "@/icons";
import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router";
import { EmptyState } from "./empty-state";

export function PageLoader() {
  return (
    <div className="flex items-center justify-center py-20 text-brand-500">
      <Spinner className="size-8" />
    </div>
  );
}

/** Redirects anonymous users to the login page */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const location = useLocation();

  if (!user) {
    const returnUrl = location.pathname + location.search;
    return <Navigate to={returnUrl === "/" ? "/login" : `/login?returnUrl=${encodeURIComponent(returnUrl)}`} replace />;
  }

  return <>{children}</>;
}

/** Shows the content only with the permission, the api enforces the same check */
export function RequirePermission({ permission, children }: { permission: string; children: ReactNode }) {
  const { can } = useAuth();
  const { t } = useT();

  if (!can(permission)) return <EmptyState icon={<LockIcon />} title={t("Forbidden.Title")} description={t("Forbidden.Description")} />;

  return <>{children}</>;
}
