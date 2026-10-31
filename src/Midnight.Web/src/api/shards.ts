import { api, toQueryString, tokens } from "./client";
import type {
  PagedQuery,
  PagedResult,
  ShardBatchImportRequest,
  ShardBatchImportResult,
  ShardCollection,
  ShardCollectionRequest,
  ShardDetail,
  ShardImportRequest,
  ShardImportResult,
  ShardRequest,
  ShardSummary,
  ShardVariable,
  ShardVariableRequest,
  ValidateVariablesRequest,
  ValidateVariablesResponse,
} from "./types";

export const shardsApi = {
  // Collections
  listCollections: (query: PagedQuery) =>
    api.get<PagedResult<ShardCollection>>(`/api/admin/shards/collections?${toQueryString(query)}`),

  listAllCollections: () =>
    api.get<ShardCollection[]>("/api/admin/shards/collections/all"),

  getCollection: (id: number) =>
    api.get<ShardCollection>(`/api/admin/shards/collections/${id}`),

  createCollection: (data: ShardCollectionRequest) =>
    api.post<ShardCollection>("/api/admin/shards/collections", data),

  updateCollection: (id: number, data: ShardCollectionRequest) =>
    api.put<ShardCollection>(`/api/admin/shards/collections/${id}`, data),

  deleteCollection: (id: number) =>
    api.delete(`/api/admin/shards/collections/${id}`),

  // Shards (Admin)
  listShards: (query: PagedQuery, collectionId?: number) => {
    const params = new URLSearchParams(toQueryString(query));
    if (collectionId !== undefined) {
      params.set("collectionId", String(collectionId));
    }
    return api.get<PagedResult<ShardSummary>>(`/api/admin/shards?${params.toString()}`);
  },

  getShard: (id: number) =>
    api.get<ShardDetail>(`/api/admin/shards/${id}`),

  createShard: (data: ShardRequest) =>
    api.post<ShardDetail>("/api/admin/shards", data),

  updateShard: (id: number, data: ShardRequest) =>
    api.put<ShardDetail>(`/api/admin/shards/${id}`, data),

  deleteShard: (id: number) =>
    api.delete(`/api/admin/shards/${id}`),

  exportShard: async (id: number, shardName: string) => {
    // Download via Fetch Blob
    const token = await tokens.getAccessToken();
    const response = await fetch(`/api/admin/shards/${id}/export`, {
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      credentials: "same-origin",
    });

    if (!response.ok) throw new Error("Export failed");

    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `shard-${shardName.toLowerCase().replace(/\s+/g, "-")}.json`;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
  },

  importShard: (data: ShardImportRequest) =>
    api.post<ShardImportResult>("/api/admin/shards/import", data),

  importShardsBatch: (data: ShardBatchImportRequest) =>
    api.post<ShardBatchImportResult>("/api/admin/shards/import-batch", data),

  uploadShardFiles: (files: File[], targetCollectionId?: number) => {
    const formData = new FormData();
    for (const file of files) {
      formData.append("files", file, file.name);
    }
    const query = targetCollectionId ? `?targetCollectionId=${targetCollectionId}` : "";
    return api.post<ShardBatchImportResult>(`/api/admin/shards/import-files${query}`, formData);
  },

  // Variablen
  listVariables: (shardId: number) =>
    api.get<ShardVariable[]>(`/api/admin/shards/${shardId}/variables`),

  createVariable: (shardId: number, data: ShardVariableRequest) =>
    api.post<ShardVariable>(`/api/admin/shards/${shardId}/variables`, data),

  updateVariable: (shardId: number, id: number, data: ShardVariableRequest) =>
    api.put<ShardVariable>(`/api/admin/shards/${shardId}/variables/${id}`, data),

  deleteVariable: (shardId: number, id: number) =>
    api.delete(`/api/admin/shards/${shardId}/variables/${id}`),

  // User area (server creation)
  listUserShards: (collectionId?: number) => {
    const url = collectionId ? `/api/shards?collectionId=${collectionId}` : "/api/shards";
    return api.get<ShardSummary[]>(url);
  },

  getUserShard: (id: number) =>
    api.get<ShardDetail>(`/api/shards/${id}`),

  validateVariables: (shardId: number, data: ValidateVariablesRequest) =>
    api.post<ValidateVariablesResponse>(`/api/shards/${shardId}/validate-variables`, data),
};
