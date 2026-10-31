import { RequirePermission } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { TabNav } from "@/components/shared/tab-nav";
import { useT } from "@/i18n";
import { GroupIcon, LockIcon, SystemIcon } from "@/icons";
import { Permissions } from "@/lib/permissions";
import { Outlet } from "react-router";

export default function UsersLayout() {
  const { t } = useT();

  return (
    <RequirePermission permission={Permissions.AdminUsersRead}>
      <PageHeader title={t("Nav.Admin.Users")} description={t("Users.Description")} />
      <TabNav
        items={[
          { label: t("Account.Overview"), to: "/admin/users", icon: <GroupIcon /> },
          { label: t("AdminUsers.Sessions"), to: "/admin/users/sessions", icon: <SystemIcon /> },
          { label: t("Nav.Admin.Roles"), to: "/admin/users/roles", icon: <LockIcon /> },
        ]}
      />
      <Outlet />
    </RequirePermission>
  );
}
