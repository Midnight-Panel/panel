import { api, toQueryString } from "@/api/client";
import type { NodeItem, NodeRequest, PagedResult } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { DataTable, type DataTableColumn } from "@/components/shared/data-table";
import { RequirePermission } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { useT } from "@/i18n";
import { ChevronRightIcon, LayerIcon, PlusIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router";
import { defaultNodeValues, NodeFormFields } from "./node-form";
import { NodeStatusBadge } from "./status-badge";

export default function NodesPage() {
  const { t } = useT();
  const { can } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const form = useForm<NodeRequest>({ mode: "onTouched", defaultValues: defaultNodeValues });

  const columns: DataTableColumn<NodeItem>[] = [
    {
      key: "name", title: t("Common.Name"), sortKey: "name",
      render: (n) => (
        <Link to={`/admin/nodes/${n.id}`} className="flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-lg bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300 [&>svg]:size-5">
            <LayerIcon />
          </span>
          <span className="font-medium text-gray-800 dark:text-white/90">{n.name}</span>
        </Link>
      ),
    },
    { key: "fqdn", title: t("Nodes.Fqdn"), sortKey: "fqdn", render: (n) => <span className="font-mono">{n.fqdn}</span> },
    {
      key: "ports", title: t("Nodes.Ports"),
      render: (n) => (
        <span className="flex items-center gap-2">
          <span className="font-mono">{n.httpPort} / {n.sftpPort}</span>
          {n.useSsl && <Badge size="sm" color="success">SSL</Badge>}
        </span>
      ),
    },
    { key: "allocations", title: t("Nodes.Allocations"), render: (n) => n.allocationCount },
    { key: "status", title: t("Common.Status"), render: (n) => <NodeStatusBadge nodeId={n.id} /> },
    {
      key: "actions", title: "", align: "end",
      render: (n) => (
        <Button variant="ghost" size="icon-sm" render={<Link to={`/admin/nodes/${n.id}`} />} nativeButton={false} aria-label={t("Common.Edit")}>
          <ChevronRightIcon />
        </Button>
      ),
    },
  ];

  const create = async (values: NodeRequest) => {
    setBusy(true);

    try {
      const node = await api.post<NodeItem>("/api/admin/nodes", values);
      await queryClient.invalidateQueries({ queryKey: ["admin", "nodes"] });
      setOpen(false);
      notifySuccess(t("Nodes.Created", node.name));
      navigate(`/admin/nodes/${node.id}/setup`);
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <RequirePermission permission={Permissions.AdminNodesRead}>
      <PageHeader title={t("Nav.Admin.Nodes")} description={t("Nodes.Description")} />

      <DataTable
        queryKey={["admin", "nodes", "list"]}
        load={(query) => api.get<PagedResult<NodeItem>>(`/api/admin/nodes?${toQueryString(query)}`)}
        columns={columns}
        rowKey={(n) => n.id}
        title={t("Nav.Admin.Nodes")}
        toolbar={can(Permissions.AdminNodesWrite) && (
          <Button onClick={() => { form.reset(defaultNodeValues); setOpen(true); }}>
            <PlusIcon />
            {t("Nodes.Create")}
          </Button>
        )}
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>{t("Nodes.Create")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={form.handleSubmit(create)} className="space-y-5" noValidate>
            <NodeFormFields form={form} />
            <DialogFooter className="mx-0 mb-0 p-0">
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("Common.Cancel")}</Button>
              <Button type="submit" disabled={busy}>{t("Nodes.Create")}</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </RequirePermission>
  );
}
