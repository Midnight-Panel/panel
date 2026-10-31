import { api } from "@/api/client";
import type { NodeStatus } from "@/api/types";
import { Badge } from "@/components/ui/badge";
import { useT } from "@/i18n";
import { useQuery } from "@tanstack/react-query";

export function useNodeStatus(nodeId: number, refetchInterval = 15_000) {
  return useQuery({
    queryKey: ["admin", "nodes", nodeId, "status"],
    queryFn: () => api.get<NodeStatus>(`/api/admin/nodes/${nodeId}/status`),
    refetchInterval,
  });
}

/** Online/offline badge, loads the live status of the node */
export function NodeStatusBadge({ nodeId }: { nodeId: number }) {
  const { t } = useT();
  const { data } = useNodeStatus(nodeId, 30_000);

  if (!data) return <Badge size="sm" color="gray">{t("Nodes.Checking")}</Badge>;

  return data.online
    ? <Badge size="sm" color="success">{t("Nodes.Online")}{data.latencyMs != null && ` · ${data.latencyMs} ms`}</Badge>
    : <Badge size="sm" color="error" title={data.error ?? undefined}>{t("Nodes.Offline")}</Badge>;
}
