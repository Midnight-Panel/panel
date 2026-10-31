import { api, toQueryString } from "@/api/client";
import type { AdminSession, PagedResult } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { DataTable, type DataTableColumn } from "@/components/shared/data-table";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { InfoCircleIcon, LogOutIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router";

export default function SessionsPage() {
  const { t } = useT();
  const { can } = useAuth();
  const confirm = useConfirm();
  const queryClient = useQueryClient();

  const ago = (value: string) => {
    const minutes = Math.floor((Date.now() - new Date(value).getTime()) / 60_000);
    if (minutes < 1) return t("Time.JustNow");
    if (minutes < 60) return t("Time.MinutesAgo", minutes);
    if (minutes < 60 * 24) return t("Time.HoursAgo", Math.floor(minutes / 60));
    return t("Time.DaysAgo", Math.floor(minutes / 60 / 24));
  };

  const revoke = async (session: AdminSession) => {
    if (!(await confirm({ title: t("Security.Revoke"), message: t("AdminUsers.RevokeConfirm", session.username), confirmLabel: t("Security.Revoke"), cancelLabel: t("Common.Cancel") })))
      return;

    try {
      await api.delete(`/api/admin/sessions/${session.id}`);
      notifySuccess(t("AdminUsers.Revoked"));
      await queryClient.invalidateQueries({ queryKey: ["admin", "sessions"] });
    } catch (error) {
      notifyError(error);
    }
  };

  const columns: DataTableColumn<AdminSession>[] = [
    {
      key: "user", title: t("Common.User"),
      render: (s) => (
        <Link to={`/admin/users/${s.userId}`}>
          <span className="block font-medium text-gray-800 dark:text-white/90">{s.username}</span>
          <span className="block text-xs">{s.email}</span>
        </Link>
      ),
    },
    { key: "device", title: t("Security.Device"), render: (s) => <span className="block max-w-xs truncate" title={s.userAgent ?? ""}>{s.userAgent ?? "-"}</span> },
    { key: "ip", title: t("Security.IpAddress"), render: (s) => <span className="font-mono">{s.ipAddress ?? "-"}</span> },
    { key: "last", title: t("AdminUsers.LastActivity"), sortKey: "lastUsedAt", render: (s) => ago(s.lastUsedAt) },
    { key: "created", title: t("AdminUsers.ConnectedSince"), sortKey: "createdAt", render: (s) => ago(s.createdAt) },
    {
      key: "actions", title: "", align: "end",
      render: (s) => can(Permissions.AdminUsersWrite) && (
        <Button variant="ghost" size="icon-sm" className="text-red-500" onClick={() => revoke(s)} aria-label={t("Security.Revoke")}>
          <LogOutIcon />
        </Button>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <Alert variant="info" className="px-4 py-3">
        <InfoCircleIcon />
        <AlertDescription>{t("AdminUsers.SessionsHint")}</AlertDescription>
      </Alert>
      <DataTable
        queryKey={["admin", "sessions"]}
        load={(query) => api.get<PagedResult<AdminSession>>(`/api/admin/sessions?${toQueryString(query)}`)}
        columns={columns}
        rowKey={(s) => s.id}
        title={t("AdminUsers.Sessions")}
      />
    </div>
  );
}
