import { useAuth } from "@/auth/AuthContext";
import AppLayout from "@/components/layout/app-layout";
import { RequireAuth } from "@/components/shared/guards";
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

        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
