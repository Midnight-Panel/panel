import { api } from "./client";
import type { PagedResult } from "./types";

// Mirror of Midnight.Shared.Http.Admin.DatabaseHostDtos – /api/admin/database-hosts (admin.servers.read / write)

export interface DatabaseHost {
  id: number;
  name: string;
  host: string;
  port: number;
  username: string;
  /** Only servers on this node use the host, null = all nodes */
  nodeId?: number | null;
  nodeName?: string | null;
  maxDatabases?: number | null;
  databaseCount: number;
  createdAt: string;
}

export interface DatabaseHostRequest {
  name: string;
  host: string;
  port: number;
  /** Needs CREATE USER and GRANT OPTION */
  username: string;
  /** Required on create, null on update keeps the stored password */
  password?: string | null;
  nodeId?: number | null;
  maxDatabases?: number | null;
}

export interface DatabaseHostTestResult {
  success: boolean;
  error?: string | null;
  version?: string | null;
}

export const databaseHostsApi = {
  /** Accepts a plain list as well as a paged result */
  list: async () => {
    const result = await api.get<DatabaseHost[] | PagedResult<DatabaseHost>>("/api/admin/database-hosts?page=1&pageSize=100");
    return Array.isArray(result) ? result : result.items;
  },
  get: (id: number) => api.get<DatabaseHost>(`/api/admin/database-hosts/${id}`),
  create: (data: DatabaseHostRequest) => api.post<DatabaseHost>("/api/admin/database-hosts", data),
  update: (id: number, data: DatabaseHostRequest) => api.put<DatabaseHost>(`/api/admin/database-hosts/${id}`, data),
  delete: (id: number) => api.delete(`/api/admin/database-hosts/${id}`),
  test: (id: number) => api.post<DatabaseHostTestResult>(`/api/admin/database-hosts/${id}/test`),
};
