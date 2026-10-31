import { useAuth } from "@/auth/AuthContext";
import GridShape from "@/components/shared/grid-shape";
import { PoweredByFooter } from "@/components/shared/powered-by-footer";
import { useBranding } from "@/contexts/BrandingContext";
import { useT } from "@/i18n";
import { safeReturnUrl } from "@/lib/returnUrl";
import { Link, Navigate, Outlet, useSearchParams } from "react-router";
import ThemeToggler from "./theme-toggler";

/** Split layout for login, register and password reset. Logged in users are sent on to the return url. */
export default function AuthLayout() {
  const { user } = useAuth();
  const { branding } = useBranding();
  const { t } = useT();
  const [params] = useSearchParams();

  if (user) return <Navigate to={safeReturnUrl(params.get("returnUrl"))} replace />;

  const logoLight = branding?.logoLightUrl || "/images/logo/logo.png";
  const logoDark = branding?.logoDarkUrl || "/images/logo/logo-dark.png";

  const bgStyle: React.CSSProperties =
    branding?.loginBackgroundType === "gradient" && branding.loginBackgroundValue
      ? { background: branding.loginBackgroundValue }
      : branding?.loginBackgroundType === "image" && branding.loginBackgroundValue
      ? { backgroundImage: `url(${branding.loginBackgroundValue})`, backgroundSize: "cover" }
      : {};

  return (
    <div className="relative z-1 min-h-screen flex flex-col bg-white dark:bg-gray-900" style={bgStyle}>
      <div className="relative flex flex-1 w-full flex-col justify-center sm:p-0 lg:flex-row">
        <div className="flex w-full flex-1 flex-col lg:w-1/2 p-6 sm:p-0">
          <div className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center py-10">
            <Link to="/" className="mb-10 lg:hidden">
              <img className="h-9 w-auto dark:hidden" src={logoLight} alt={branding?.panelName || "Midnight"} />
              <img className="hidden h-9 w-auto dark:block" src={logoDark} alt={branding?.panelName || "Midnight"} />
            </Link>
            <Outlet />
          </div>
        </div>

        <div className="relative hidden h-screen w-full items-center justify-center bg-brand-950 lg:sticky lg:top-0 lg:grid lg:w-1/2 dark:bg-white/5">
          <div className="z-1 flex h-full items-center justify-center">
            <GridShape />
            <div className="flex max-w-xs flex-col items-center">
              <img src={logoDark} alt={branding?.panelName || "Midnight"} className="mb-4 h-11 w-auto object-contain" />
              <p className="text-center text-gray-400 dark:text-white/60">{t("Auth.Tagline")}</p>
            </div>
          </div>
        </div>

        <div className="fixed inset-e-6 bottom-14 z-50 hidden sm:block">
          <ThemeToggler />
        </div>
      </div>

      {/* Unveränderbarer Footer auf Login/Auth-Seiten */}
      <div className="py-4 flex justify-center border-t border-gray-200/80 dark:border-white/5 bg-white/40 dark:bg-black/20 backdrop-blur-xs">
        <PoweredByFooter />
      </div>
    </div>
  );
}
