import { useAuth } from "@/auth/AuthContext";
import AppLayout from "@/components/layout/app-layout";
import { RequireAuth, RequirePermission } from "@/components/shared/guards";
import { Permissions } from "@/lib/permissions";
import { Spinner } from "@/components/ui/spinner";
import AccountLayout from "@/pages/account/layout";
import ApiKeysPage from "@/pages/account/api-keys";
import ProfilePage from "@/pages/account/profile";
import SecurityPage from "@/pages/account/security";
import NodeAllocationsPage from "@/pages/admin/nodes/allocations";
import NodeLayout from "@/pages/admin/nodes/layout";
import NodesPage from "@/pages/admin/nodes/list";
import NodeLogsPage from "@/pages/admin/nodes/logs";
import NodeOverviewPage from "@/pages/admin/nodes/overview";
import NodeSettingsPage from "@/pages/admin/nodes/settings";
import NodeSetupPage from "@/pages/admin/nodes/setup";
import AdminOverviewPage from "@/pages/admin/overview";
import AdminSettingsPage from "@/pages/admin/settings";
import AdminCustomizerPage from "@/pages/admin/customizer";
import AdminServerBuildPage from "@/pages/admin/servers/build";
import CreateServerPage from "@/pages/admin/servers/create";
import AdminServerDetailsPage from "@/pages/admin/servers/details";
import AdminServerLayout from "@/pages/admin/servers/layout";
import AdminServersPage from "@/pages/admin/servers/list";
import DatabaseHostsPage from "@/pages/admin/database-hosts";
import AdminServerManagePage from "@/pages/admin/servers/manage";
import AdminServerOverviewPage from "@/pages/admin/servers/overview";
import AdminServerStartupPage from "@/pages/admin/servers/startup";
import ShardDetailPage from "@/pages/admin/shards/detail";
import ShardsPage from "@/pages/admin/shards/list";
import ExtensionDetailPage from "@/pages/admin/extensions/detail";
import ExtensionsPage from "@/pages/admin/extensions/list";
import UserEditPage from "@/pages/admin/users/edit";
import UsersLayout from "@/pages/admin/users/layout";
import UsersPage from "@/pages/admin/users/list";
import RolesPage from "@/pages/admin/users/roles";
import SessionsPage from "@/pages/admin/users/sessions";
import ForgotPasswordPage from "@/pages/auth/forgot-password";
import LoginPage from "@/pages/auth/login";
import RegisterPage from "@/pages/auth/register";
import ResetPasswordPage from "@/pages/auth/reset-password";
import DashboardPage from "@/pages/dashboard";
import NotFoundPage from "@/pages/not-found";
import ServerActivityPage from "@/pages/servers/activity";
import ServerBackupsPage from "@/pages/servers/backups";
import ServerConsolePage from "@/pages/servers/console";
import ServerDatabasesPage from "@/pages/servers/databases";
import ServerNetworkPage from "@/pages/servers/network";
import ServerScheduleDetailPage from "@/pages/servers/schedules/detail";
import ServerSchedulesPage from "@/pages/servers/schedules";
import ServerUsersPage from "@/pages/servers/users";
import ServerFileEditorPage from "@/pages/servers/files/editor";
import ServerFilesPage from "@/pages/servers/files";
import ServerLayout from "@/pages/servers/layout";
import ServerSettingsPage from "@/pages/servers/settings";
import ServerStartupPage from "@/pages/servers/startup";
import AuthLayout from "@/pages/others/auth/layout";
import { Route, Routes } from "react-router";

export default function App() {
  const { ready } = useAuth();

  if (!ready)
    return (
      <div className="flex h-screen items-center justify-center text-brand-500">
        <Spinner className="size-10" />
      </div>
    );

  return (
    <Routes>
      <Route element={<AuthLayout />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
      </Route>

      <Route element={<RequireAuth><AppLayout /></RequireAuth>}>
        <Route index element={<DashboardPage />} />

        <Route path="servers/:uuid" element={<ServerLayout />}>
          <Route index element={<ServerConsolePage />} />
          <Route path="files" element={<ServerFilesPage />} />
          <Route path="files/edit" element={<ServerFileEditorPage />} />
          <Route path="databases" element={<ServerDatabasesPage />} />
          <Route path="schedules" element={<ServerSchedulesPage />} />
          <Route path="schedules/:scheduleId" element={<ServerScheduleDetailPage />} />
          <Route path="users" element={<ServerUsersPage />} />
          <Route path="backups" element={<ServerBackupsPage />} />
          <Route path="network" element={<ServerNetworkPage />} />
          <Route path="startup" element={<ServerStartupPage />} />
          <Route path="settings" element={<ServerSettingsPage />} />
          <Route path="activity" element={<ServerActivityPage />} />
        </Route>

        <Route path="account" element={<AccountLayout />}>
          <Route index element={<ProfilePage />} />
          <Route path="security" element={<SecurityPage />} />
          <Route path="api-keys" element={<ApiKeysPage />} />
        </Route>

        <Route path="admin" element={<AdminOverviewPage />} />
        <Route path="admin/users" element={<UsersLayout />}>
          <Route index element={<UsersPage />} />
          <Route path="sessions" element={<SessionsPage />} />
          <Route path="roles" element={<RolesPage />} />
          <Route path=":id" element={<UserEditPage />} />
        </Route>

        <Route path="admin/nodes" element={<NodesPage />} />
        <Route path="admin/nodes/:id" element={<NodeLayout />}>
          <Route index element={<NodeOverviewPage />} />
          <Route path="allocations" element={<NodeAllocationsPage />} />
          <Route path="setup" element={<NodeSetupPage />} />
          <Route path="logs" element={<NodeLogsPage />} />
          <Route path="settings" element={<NodeSettingsPage />} />
        </Route>

        <Route path="admin/servers" element={<AdminServersPage />} />
        <Route path="admin/database-hosts" element={<DatabaseHostsPage />} />
        <Route path="admin/servers/create" element={<CreateServerPage />} />
        <Route path="admin/servers/:id" element={<AdminServerLayout />}>
          <Route index element={<AdminServerOverviewPage />} />
          <Route path="details" element={<RequirePermission permission={Permissions.AdminServersWrite}><AdminServerDetailsPage /></RequirePermission>} />
          <Route path="build" element={<RequirePermission permission={Permissions.AdminServersWrite}><AdminServerBuildPage /></RequirePermission>} />
          <Route path="startup" element={<RequirePermission permission={Permissions.AdminServersWrite}><AdminServerStartupPage /></RequirePermission>} />
          <Route path="manage" element={<RequirePermission permission={Permissions.AdminServersWrite}><AdminServerManagePage /></RequirePermission>} />
        </Route>

        <Route path="admin/shards" element={<ShardsPage />} />
        <Route path="admin/shards/:id" element={<ShardDetailPage />} />
        <Route path="admin/extensions" element={<ExtensionsPage />} />
        <Route path="admin/extensions/:id" element={<ExtensionDetailPage />} />
        <Route path="admin/settings" element={<AdminSettingsPage />} />
        <Route
          path="admin/customizer"
          element={
            <RequirePermission permission={Permissions.AdminSystemWrite}>
              <AdminCustomizerPage />
            </RequirePermission>
          }
        />

        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
