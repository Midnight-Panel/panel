import { useAuth } from "@/auth/AuthContext";
import { useBranding } from "@/contexts/BrandingContext";
import AppHeader from "@/components/layout/app-layout/header";
import AppSidebar from "@/components/layout/app-layout/sidebar";
import { ScrollToTop } from "@/components/shared/scroll-to-top";
import { Alert, AlertAction, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";
import { useT } from "@/i18n";
import { EyeIcon, OctagonAlertIcon } from "@/icons";
import { PoweredByFooter } from "@/components/shared/powered-by-footer";
import { isSafeLink } from "@/lib/returnUrl";
import { Outlet, useNavigate } from "react-router";

export default function AppLayout() {
  const { user, settings, isImpersonating, stopImpersonation } = useAuth();
  const { t } = useT();
  const navigate = useNavigate();

  const { branding } = useBranding();

  const rawLinks =
    branding?.footerLinks && branding.footerLinks.length > 0
      ? branding.footerLinks
      : settings.footerLinks && settings.footerLinks.length > 0
        ? settings.footerLinks
        : [
            { id: "api-ref", label: t("Footer.Api"), url: "/api/reference", newTab: true },
            { id: "github", label: "GitHub", url: "https://github.com/Midnight-Panel/panel", newTab: true },
          ];

  // Strict dedupe: each link (by URL or ID) appears only once
  const seenLinks = new Set<string>();
  const footerLinks = rawLinks.filter((link) => {
    const key = (link.url || link.id || "").toLowerCase().trim();
    if (!key || seenLinks.has(key) || !isSafeLink(link.url)) return false;
    seenLinks.add(key);
    return true;
  });

  return (
    <SidebarProvider>
      <AppSidebar />

      <SidebarInset>
        <AppHeader />

        <div className="mx-auto flex w-full max-w-(--breakpoint-2xl) flex-1 flex-col p-4 md:p-6">
          {settings.maintenanceMode && (
            <Alert variant="warning" className="mb-6 px-4 py-3">
              <OctagonAlertIcon />
              <AlertDescription className="text-gray-700 dark:text-gray-300">{t("Footer.Maintenance")}</AlertDescription>
            </Alert>
          )}

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

          <footer className="mt-auto flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-t border-gray-200 pt-5 text-sm text-gray-500 dark:border-gray-800 dark:text-gray-400">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
              <span>© {new Date().getFullYear()} {branding?.panelName || settings.appName || "Midnight Panel"} · v{settings.version}</span>
              {footerLinks.map((link) => (
                <a
                  key={link.id || link.url}
                  href={link.url}
                  target={link.newTab ? "_blank" : undefined}
                  rel={link.newTab ? "noreferrer" : undefined}
                  className="hover:text-brand-500 transition-colors"
                >
                  {link.label}
                </a>
              ))}
            </div>

            <PoweredByFooter />
          </footer>
        </div>
      </SidebarInset>
      <ScrollToTop />
    </SidebarProvider>
  );
}
