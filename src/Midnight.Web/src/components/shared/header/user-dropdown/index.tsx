import { useAuth } from "@/auth/AuthContext";
import { UserAvatar } from "@/components/shared/user-avatar";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuPortal,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown";
import { useChangeLocale, useLocale, useT } from "@/i18n";
import { getLanguage, languages } from "@/i18n/languages";
import type { Locale } from "@/i18n/routing";
import { ChevronDownIcon, GlobeIcon, KeyIcon, LockIcon, LogOutIcon, UserCircleIcon } from "@/icons";
import { cn } from "@/utils";
import { useState } from "react";
import { Link, useNavigate } from "react-router";

export default function UserDropdown() {
  const { t } = useT();
  const { user, logout, isImpersonating } = useAuth();
  const locale = useLocale() as Locale;
  const changeLocale = useChangeLocale();
  const navigate = useNavigate();
  const [isOpen, setIsOpen] = useState(false);

  if (!user) return null;

  const currentLang = getLanguage(locale);
  const CurrentFlagIcon = currentLang.FlagIcon;

  const handleLogout = async () => {
    const wasImpersonating = isImpersonating;
    await logout();
    navigate(wasImpersonating ? "/admin/users" : "/login");
  };

  return (
    <DropdownMenu open={isOpen} onOpenChange={setIsOpen}>
      <DropdownMenuTrigger className="dropdown-toggle flex items-center text-gray-700 dark:text-gray-400">
        <span className="me-3">
          <UserAvatar userId={user.id} hasAvatar={user.hasAvatar} name={user.username} size="lg" />
        </span>

        <span className="me-1 block text-sm font-medium">{user.username}</span>

        <ChevronDownIcon
          className={cn("size-5 text-gray-500 duration-200 dark:text-gray-400", isOpen ? "rotate-180" : "rotate-0")}
        />
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" sideOffset={12} className="w-65 p-3">
        <div className="px-3 py-2">
          <span className="block text-sm font-medium text-gray-700 dark:text-gray-400">{user.username}</span>
          <span className="mt-0.5 block text-xs text-gray-500 dark:text-gray-400">{user.email}</span>
        </div>

        <DropdownMenuSeparator />

        <DropdownMenuItem render={<Link to="/account" />}>
          <UserCircleIcon className="size-6" />
          {t("Nav.Account")}
        </DropdownMenuItem>

        <DropdownMenuItem render={<Link to="/account/security" />}>
          <LockIcon className="size-6" />
          {t("Nav.Security")}
        </DropdownMenuItem>

        <DropdownMenuItem render={<Link to="/account/api-keys" />}>
          <KeyIcon className="size-6" />
          {t("Nav.ApiKeys")}
        </DropdownMenuItem>

        <DropdownMenuSub>
          <DropdownMenuSubTrigger>
            <GlobeIcon className="size-6" />
            <span>{t("Header.Language")}</span>
            <Badge
              variant="light"
              color="gray"
              size="sm"
              className="ms-auto me-1 h-auto gap-1.5 border border-gray-200 bg-gray-50 px-2 py-1 text-xs font-medium text-gray-700 text-shadow-none dark:border-gray-800 dark:bg-white/3 dark:text-gray-300 dark:text-shadow-none [&>svg]:size-3.5"
            >
              <span>{currentLang.shortName}</span>
              <CurrentFlagIcon className="size-3.5 shrink-0 overflow-hidden rounded-full" />
            </Badge>
          </DropdownMenuSubTrigger>

          <DropdownMenuPortal>
            <DropdownMenuSubContent className="w-56 p-2">
              {languages.map((language) => {
                const isSelected = locale === language.id;
                const FlagIcon = language.FlagIcon;

                return (
                  <DropdownMenuItem
                    key={language.id}
                    onClick={() => changeLocale(language.id)}
                    className={cn(
                      "flex items-center justify-between gap-2",
                      isSelected && "bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-400",
                    )}
                  >
                    <span className="flex items-center gap-2">
                      <span
                        className={cn(
                          "size-1.5 shrink-0 rounded-full transition-opacity",
                          isSelected ? "bg-brand-500 opacity-100 dark:bg-brand-400" : "opacity-0",
                        )}
                      />
                      <FlagIcon className="size-5 shrink-0 overflow-hidden rounded-full" />
                      <span className="truncate">{language.name}</span>
                    </span>
                  </DropdownMenuItem>
                );
              })}
            </DropdownMenuSubContent>
          </DropdownMenuPortal>
        </DropdownMenuSub>

        <DropdownMenuSeparator />

        <DropdownMenuItem variant="destructive" onClick={handleLogout}>
          <LogOutIcon />
          {isImpersonating ? t("Impersonation.Stop") : t("Header.Logout")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
