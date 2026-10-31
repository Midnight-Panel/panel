import { adminServersApi } from "@/api/servers";
import type { AdminServerSummary } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { DataTable, type DataTableColumn } from "@/components/shared/data-table";
import { RequirePermission } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { ChevronRightIcon, ChipIcon, PlusIcon } from "@/icons";
import { Permissions } from "@/lib/permissions";
import { limitsSummary } from "@/lib/servers";
import { ServerStatusBadge } from "@/pages/servers/components/badges";
import { Link } from "react-router";

export default function AdminServersPage() {
  const { t } = useT();
  const { can } = useAuth();

  const columns: DataTableColumn<AdminServerSummary>[] = [
    {
      key: "name", title: t("Common.Name"), sortKey: "name",
      render: (server) => (
        <Link to={`/admin/servers/${server.id}`} className="flex items-center gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300 [&>svg]:size-5">
            <ChipIcon />
          </span>
          <span className="min-w-0">
            <span className="block truncate font-medium text-gray-800 dark:text-white/90">{server.name}</span>
            <span className="block font-mono text-xs text-gray-400 dark:text-gray-500">{server.uuid.slice(0, 8)}</span>
          </span>
        </Link>
      ),
    },
    {
      key: "owner", title: t("Servers.Owner"),
      render: (server) => <Link to={`/admin/users/${server.ownerId}`} className="hover:text-brand-500">{server.ownerUsername}</Link>,
    },
    {
      key: "node", title: t("Servers.Node"),
      render: (server) => <Link to={`/admin/nodes/${server.nodeId}`} className="hover:text-brand-500">{server.nodeName}</Link>,
    },
    { key: "shard", title: t("Servers.Shard"), render: (server) => server.shardName },
    {
      key: "allocation", title: t("Servers.Address"),
      render: (server) => <span className="font-mono">{server.primaryAllocation ?? "-"}</span>,
    },
    { key: "limits", title: t("Servers.Limits"), render: (server) => <span className="whitespace-nowrap">{limitsSummary(server.limits, t)}</span> },
    { key: "status", title: t("Common.Status"), render: (server) => <ServerStatusBadge status={server.status} suspended={server.suspended} /> },
    {
      key: "actions", title: "", align: "end",
      render: (server) => (
        <Button variant="ghost" size="icon-sm" render={<Link to={`/admin/servers/${server.id}`} />} nativeButton={false} aria-label={t("Common.Edit")}>
          <ChevronRightIcon />
        </Button>
      ),
    },
  ];

  return (
    <RequirePermission permission={Permissions.AdminServersRead}>
      <PageHeader icon={<ChipIcon />} title={t("Nav.Admin.Servers")} description={t("Servers.AdminDescription")} />

      <DataTable
        queryKey={["admin", "servers", "list"]}
        load={adminServersApi.list}
        columns={columns}
        rowKey={(server) => server.id}
        title={t("Nav.Admin.Servers")}
        toolbar={can(Permissions.AdminServersWrite) && (
          <Button render={<Link to="/admin/servers/create" />} nativeButton={false}>
            <PlusIcon />
            {t("Servers.Create")}
          </Button>
        )}
      />
    </RequirePermission>
  );
}
