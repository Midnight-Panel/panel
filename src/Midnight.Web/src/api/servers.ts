import { api, toQueryString } from "./client";
import type {
  AdminServerDetail,
  AdminServerSummary,
  AdminUser,
  Allocation,
  ConsoleTicket,
  CreateServerRequest,
  NodeItem,
  PagedQuery,
  PagedResult,
  PowerAction,
  RenameServerRequest,
  ServerDetail,
  ServerResources,
  ServerSummary,
  UpdateServerBuildRequest,
  UpdateServerDetailsRequest,
  UpdateServerStartupRequest,
  UpdateServerVariablesRequest,
} from "./types";

/** Admin api: /api/admin/servers (admin.servers.read / admin.servers.write) */
export const adminServersApi = {
  list: (query: PagedQuery) => api.get<PagedResult<AdminServerSummary>>(`/api/admin/servers?${toQueryString(query)}`),

  get: (id: number) => api.get<AdminServerDetail>(`/api/admin/servers/${id}`),

  create: (data: CreateServerRequest) => api.post<AdminServerDetail>("/api/admin/servers", data),

  updateDetails: (id: number, data: UpdateServerDetailsRequest) =>
    api.put<AdminServerDetail>(`/api/admin/servers/${id}/details`, data),

  updateBuild: (id: number, data: UpdateServerBuildRequest) =>
    api.put<AdminServerDetail>(`/api/admin/servers/${id}/build`, data),

  updateStartup: (id: number, data: UpdateServerStartupRequest) =>
    api.put<AdminServerDetail>(`/api/admin/servers/${id}/startup`, data),

  suspend: (id: number) => api.post(`/api/admin/servers/${id}/suspend`),

  unsuspend: (id: number) => api.post(`/api/admin/servers/${id}/unsuspend`),

  reinstall: (id: number) => api.post(`/api/admin/servers/${id}/reinstall`),

  delete: (id: number, force = false) => api.delete(`/api/admin/servers/${id}?force=${force}`),

  // Helpers of the create / build forms (existing endpoints)

  searchUsers: (search: string) =>
    api.get<PagedResult<AdminUser>>(`/api/admin/users?${toQueryString({ page: 1, pageSize: 20, search: search || undefined })}`),

  listNodes: () => api.get<PagedResult<NodeItem>>(`/api/admin/nodes?${toQueryString({ page: 1, pageSize: 100, sortBy: "name" })}`),

  /** Allocations of the node without a server */
  freeAllocations: (nodeId: number, search?: string) =>
    api.get<PagedResult<Allocation>>(
      `/api/admin/nodes/${nodeId}/allocations?${toQueryString({ page: 1, pageSize: 100, search: search || undefined })}&free=true`,
    ),
};

/** User api: /api/servers (owner, or admins with admin.servers.read) */
export const serversApi = {
  list: (all = false) => api.get<ServerSummary[]>(all ? "/api/servers?all=true" : "/api/servers"),

  get: (uuid: string) => api.get<ServerDetail>(`/api/servers/${uuid}`),

  resources: (uuid: string) => api.get<ServerResources>(`/api/servers/${uuid}/resources`),

  /** Live values of all servers of the list, keyed by uuid */
  allResources: (all = false) =>
    api.get<Record<string, ServerResources>>(all ? "/api/servers/resources?all=true" : "/api/servers/resources"),

  power: (uuid: string, action: PowerAction) => api.post(`/api/servers/${uuid}/power`, { action }),

  command: (uuid: string, command: string) => api.post(`/api/servers/${uuid}/command`, { command }),

  rename: (uuid: string, data: RenameServerRequest) => api.put<ServerDetail>(`/api/servers/${uuid}/rename`, data),

  updateVariables: (uuid: string, data: UpdateServerVariablesRequest) =>
    api.put<ServerDetail>(`/api/servers/${uuid}/variables`, data),

  reinstall: (uuid: string) => api.post(`/api/servers/${uuid}/reinstall`),

  consoleTicket: (uuid: string) => api.post<ConsoleTicket>(`/api/servers/${uuid}/console-ticket`),
};

/** Absolute websocket url of a relative path of the panel */
export function websocketUrl(path: string) {
  return `${location.protocol === "https:" ? "wss" : "ws"}://${location.host}${path}`;
}
