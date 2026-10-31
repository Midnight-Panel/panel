import { useAuth } from "@/auth/AuthContext";
import AppHeader from "@/components/layout/app-layout/header";
import AppSidebar from "@/components/layout/app-layout/sidebar";
import { ScrollToTop } from "@/components/shared/scroll-to-top";
import { Alert, AlertAction, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";
import { useT } from "@/i18n";
import { EyeIcon } from "@/icons";
import { Outlet, useNavigate } from "react-router";

export default function AppLayout() {
  const { user, settings, isImpersonating, stopImpersonation } = useAuth();
  const { t } = useT();
  const navigate = useNavigate();

  return (
    <SidebarProvider>
      <AppSidebar />

      <SidebarInset>
        <AppHeader />

        <div className="mx-auto flex w-full max-w-(--breakpoint-2xl) flex-1 flex-col p-4 md:p-6">
          {isImpersonating && user && (
            <Alert variant="warning" className="mb-6 px-4 py-3">
              <EyeIcon />
              <AlertDescription className="text-gray-700 dark:text-gray-300">{t("Impersonation.Banner", user.username)}</AlertDescription>
              <AlertAction className="top-1.5">
                <Button size="xs" variant="outline" onClick={async () => { await stopImpersonation(); navigate("/admin/users"); }}>
                  {t("Impersonation.Stop")}
                </Button>
              </AlertAction>
            </Alert>
          )}

          <div className="mb-10">
            <Outlet />
          </div>

          <footer className="mt-auto flex flex-wrap items-center justify-between gap-2 border-t border-gray-200 pt-5 text-sm text-gray-500 dark:border-gray-800 dark:text-gray-400">
            <span>© {new Date().getFullYear()} Midnight Panel · v{settings.version}</span>
            <span className="flex gap-4">
              <a href="/api/reference" target="_blank" className="hover:text-brand-500">{t("Footer.Api")}</a>
              <a href="https://github.com/Midnight-Panel/panel" target="_blank" rel="noreferrer" className="hover:text-brand-500">GitHub</a>
            </span>
          </footer>
        </div>
      </SidebarInset>
      <ScrollToTop />
    </SidebarProvider>
  );
}
