import ThemeToggleButton from "@/components/shared/header/theme-toggler";
import UserDropdown from "@/components/shared/header/user-dropdown";
import { Button } from "@/components/ui/button";
import { useSidebar } from "@/components/ui/sidebar";
import { useT } from "@/i18n";
import { ArrowLeftIcon, HorizontalDotsIcon, MenuTwoIcon, XIcon } from "@/icons";
import { cn } from "@/utils";
import { useState } from "react";
import { Link, useLocation } from "react-router";
import { serverUuidFromPath } from "@/pages/servers/navigation";
import { PageSearch } from "./page-search";

export default function AppHeader() {
  const { t } = useT();
  const { pathname } = useLocation();
  const [isApplicationMenuOpen, setApplicationMenuOpen] = useState(false);
  const { openMobile, toggleSidebar } = useSidebar();

  const isServer = Boolean(serverUuidFromPath(pathname));

  const toggleApplicationMenu = () => {
    setApplicationMenuOpen(!isApplicationMenuOpen);
  };

  return (
    <header className="sticky top-0 z-50 flex w-full shrink-0 border-gray-200 bg-white xl:border-b dark:border-gray-800 dark:bg-gray-900">
      <div className="flex grow flex-col items-center justify-between xl:flex-row xl:px-6">
        {/* Left side content */}
        <div className="flex w-full items-center justify-between gap-2 border-b border-gray-200 px-3 py-3 sm:gap-4 xl:justify-normal xl:border-b-0 xl:px-0 xl:py-4 dark:border-gray-800">
          <Button
            variant="outline"
            size="icon"
            className="text-gray-500 lg:size-11 dark:text-gray-400"
            onClick={toggleSidebar}
            aria-label={t("Header.ToggleSidebar")}
          >
            {openMobile ? (
              <XIcon className="size-6" />
            ) : (
              <MenuTwoIcon className="size-auto! rtl:-scale-x-100" />
            )}
          </Button>

          {isServer && (
            <Link
              to="/"
              className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 bg-gray-50/80 px-2.5 py-1.5 text-xs font-medium text-gray-700 transition-colors hover:border-brand-500/30 hover:bg-brand-500/5 hover:text-brand-500 dark:border-gray-800 dark:bg-gray-800/60 dark:text-gray-300 dark:hover:border-brand-500/30 dark:hover:bg-brand-500/10 dark:hover:text-brand-400"
              title={t("Nav.BackToDashboard")}
            >
              <ArrowLeftIcon className="size-3.5" />
              <span>{t("Nav.BackToDashboard")}</span>
            </Link>
          )}

          <Link to="/" className="xl:hidden">
            <img
              className="h-7 w-auto dark:hidden"
              src="/images/logo/logo.png"
              alt="Midnight"
            />
            <img
              className="hidden h-7 w-auto dark:block"
              src="/images/logo/logo-dark.png"
              alt="Midnight"
            />
          </Link>

          <Button
            variant="ghost"
            size="icon"
            onClick={toggleApplicationMenu}
            aria-label="Toggle application menu"
            className="text-gray-700 xl:hidden dark:text-gray-400"
          >
            <HorizontalDotsIcon />
          </Button>

          <div className="hidden xl:block">
            <PageSearch />
          </div>
        </div>

        {/* Right side content */}
        <div
          className={cn(
            "flex w-full items-center justify-between gap-4 px-5 py-4 shadow-theme-md xl:flex xl:justify-end xl:px-0 xl:shadow-none",
            isApplicationMenuOpen ? "flex" : "hidden",
          )}
        >
          <div className="flex items-center gap-2 2xsm:gap-3">
            <ThemeToggleButton />
          </div>

          <UserDropdown />
        </div>
      </div>
    </header>
  );
}
