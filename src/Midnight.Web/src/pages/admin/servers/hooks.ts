import { adminServersApi } from "@/api/servers";
import { shardsApi } from "@/api/shards";
import type { AdminServerDetail } from "@/api/types";
import { useQuery } from "@tanstack/react-query";
import { useOutletContext } from "react-router";

export function useShardList() {
  return useQuery({
    queryKey: ["admin", "shards", "list", "all"],
    queryFn: () => shardsApi.listShards({ page: 1, pageSize: 100, sortBy: "name" }),
  });
}

export function useShardDetail(shardId: number | null) {
  return useQuery({
    queryKey: ["admin", "shards", shardId],
    queryFn: () => shardsApi.getShard(shardId!),
    enabled: shardId !== null,
  });
}

/** The server of the admin detail layout */
export function useAdminServer() {
  return useOutletContext<AdminServerDetail>();
}

export function adminServerQueryKey(id: number) {
  return ["admin", "servers", id] as const;
}

export function useAdminServerQuery(id: number) {
  return useQuery({ queryKey: adminServerQueryKey(id), queryFn: () => adminServersApi.get(id) });
}
