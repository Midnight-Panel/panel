import { useAuth } from "@/auth/AuthContext";
import { PageHeader } from "@/components/shared/page-header";
import { TabNav } from "@/components/shared/tab-nav";
import { UserAvatar } from "@/components/shared/user-avatar";
import { Badge } from "@/components/ui/badge";
import { useT } from "@/i18n";
import { KeyIcon, LockIcon, UserCircleIcon } from "@/icons";
import { Outlet } from "react-router";

export default function AccountLayout() {
  const { user } = useAuth();
  const { t } = useT();

  if (!user) return null;

  return (
    <>
      <PageHeader title={t("Nav.Account")} />

      <div className="mb-6 rounded-2xl border border-gray-200 bg-white p-5 lg:p-6 dark:border-gray-800 dark:bg-white/3">
        <div className="flex flex-col items-center gap-5 sm:flex-row">
          <UserAvatar userId={user.id} hasAvatar={user.hasAvatar} name={user.username} className="size-20! text-xl" />
          <div className="text-center sm:text-start">
            <h4 className="text-lg font-semibold text-gray-800 dark:text-white/90">@{user.username}</h4>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{user.email}</p>
          </div>
          <div className="flex gap-2 sm:ms-auto">
            {user.twoFactorEnabled ? <Badge color="success">2FA</Badge> : <Badge color="warning">{t("Security.TwoFactorOff")}</Badge>}
          </div>
        </div>
      </div>

      <TabNav
        items={[
          { label: t("Account.Overview"), to: "/account", icon: <UserCircleIcon /> },
          { label: t("Nav.Security"), to: "/account/security", icon: <LockIcon /> },
          { label: t("Nav.ApiKeys"), to: "/account/api-keys", icon: <KeyIcon /> },
        ]}
      />

      <Outlet />
    </>
  );
}
