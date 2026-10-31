import { api } from "@/api/client";
import type { NodeItem } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { PageLoader, RequirePermission } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { TabNav } from "@/components/shared/tab-nav";
import { Badge } from "@/components/ui/badge";
import { useT } from "@/i18n";
import { CodeIcon, DocIcon, GearIcon, LayerIcon, LayoutGridIcon, ListIcon } from "@/icons";
import { notifyError } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { Outlet, useNavigate, useOutletContext, useParams } from "react-router";
import { NodeStatusBadge } from "./status-badge";

export function useNode() {
  return useOutletContext<NodeItem>();
}

function NodeContent() {
  const { id } = useParams();
  const { t } = useT();
  const { can } = useAuth();
  const navigate = useNavigate();

  const node = useQuery({ queryKey: ["admin", "nodes", Number(id)], queryFn: () => api.get<NodeItem>(`/api/admin/nodes/${id}`) });

  useEffect(() => {
    if (node.error) {
      notifyError(node.error);
      navigate("/admin/nodes");
    }
  }, [node.error, navigate]);

  if (!node.data) return <PageLoader />;

  const base = `/admin/nodes/${node.data.id}`;

  return (
    <>
      <PageHeader
        icon={<LayerIcon />}
        title={node.data.name}
        description={`${node.data.useSsl ? "https" : "http"}://${node.data.fqdn}:${node.data.httpPort}`}
        crumbs={[{ label: t("Nav.Admin.Nodes"), to: "/admin/nodes" }]}
        actions={
          <span className="flex items-center gap-2">
            {node.data.useSsl && <Badge size="sm" color="success">SSL</Badge>}
            <NodeStatusBadge nodeId={node.data.id} />
          </span>
        }
      />

      <TabNav
        items={[
          { label: t("Account.Overview"), to: base, icon: <LayoutGridIcon /> },
          { label: t("Nodes.Allocations"), to: `${base}/allocations`, icon: <ListIcon /> },
          ...(can(Permissions.AdminNodesWrite) ? [{ label: t("Nodes.Setup"), to: `${base}/setup`, icon: <CodeIcon /> }] : []),
          { label: t("Nodes.Logs"), to: `${base}/logs`, icon: <DocIcon /> },
          ...(can(Permissions.AdminNodesWrite) ? [{ label: t("Nodes.Settings"), to: `${base}/settings`, icon: <GearIcon /> }] : []),
        ]}
      />

      <Outlet context={node.data} />
    </>
  );
}

export default function NodeLayout() {
  return (
    <RequirePermission permission={Permissions.AdminNodesRead}>
      <NodeContent />
    </RequirePermission>
  );
}
