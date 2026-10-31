import { useAuth } from "@/auth/AuthContext";
import { UserAvatar } from "@/components/shared/user-avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown";
import { useT } from "@/i18n";
import { ChevronDownIcon, KeyIcon, LockIcon, LogOutIcon, UserCircleIcon } from "@/icons";
import { cn } from "@/utils";
import { useState } from "react";
import { Link, useNavigate } from "react-router";

export default function UserDropdown() {
  const { t } = useT();
  const { user, logout, isImpersonating } = useAuth();
  const navigate = useNavigate();
  const [isOpen, setIsOpen] = useState(false);

  if (!user) return null;

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

        <DropdownMenuSeparator />

        <DropdownMenuItem variant="destructive" onClick={handleLogout}>
          <LogOutIcon />
          {isImpersonating ? t("Impersonation.Stop") : t("Header.Logout")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
