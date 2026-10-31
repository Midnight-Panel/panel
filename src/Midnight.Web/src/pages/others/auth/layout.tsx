import { useAuth } from "@/auth/AuthContext";
import GridShape from "@/components/shared/grid-shape";
import { useT } from "@/i18n";
import { safeReturnUrl } from "@/lib/returnUrl";
import { Link, Navigate, Outlet, useSearchParams } from "react-router";
import ThemeToggler from "./theme-toggler";

/** Split layout for login, register and password reset. Logged in users are sent on to the return url. */
export default function AuthLayout() {
  const { user } = useAuth();
  const { t } = useT();
  const [params] = useSearchParams();

  if (user) return <Navigate to={safeReturnUrl(params.get("returnUrl"))} replace />;

  return (
    <div className="relative z-1 bg-white p-6 sm:p-0 dark:bg-gray-900">
      <div className="relative flex min-h-screen w-full flex-col justify-center sm:p-0 lg:flex-row dark:bg-gray-900">
        <div className="flex w-full flex-1 flex-col lg:w-1/2">
          <div className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center py-10">
            <Link to="/" className="mb-10 lg:hidden">
              <img className="h-9 w-auto dark:hidden" src="/images/logo/logo.png" alt="Midnight" />
              <img className="hidden h-9 w-auto dark:block" src="/images/logo/logo-dark.png" alt="Midnight" />
            </Link>
            <Outlet />
          </div>
        </div>

        <div className="relative hidden h-screen w-full items-center justify-center bg-brand-950 lg:sticky lg:top-0 lg:grid lg:w-1/2 dark:bg-white/5">
          <div className="z-1 flex h-full items-center justify-center">
            <GridShape />
            <div className="flex max-w-xs flex-col items-center">
              <img src="/images/logo/logo-dark.png" alt="Midnight" className="mb-4 h-11 w-auto" />
              <p className="text-center text-gray-400 dark:text-white/60">{t("Auth.Tagline")}</p>
            </div>
          </div>
        </div>

        <div className="fixed inset-e-6 bottom-6 z-50 hidden sm:block">
          <ThemeToggler />
        </div>
      </div>
    </div>
  );
}
