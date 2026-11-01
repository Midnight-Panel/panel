import { api } from "./client";

// Mirror of Midnight.Shared.Http.Application.ApplicationKeyModels – /api/admin/application-keys (admin.api.read / write)

export const ApplicationResources = ["Users", "Servers", "Nodes", "Allocations", "Shards", "DatabaseHosts"] as const;
export type ApplicationResource = (typeof ApplicationResources)[number];

export const ApplicationAccessLevels = ["None", "Read", "ReadWrite"] as const;
export type ApplicationAccess = (typeof ApplicationAccessLevels)[number];

/** Every resource is present in responses, missing ones in requests count as None */
export type ApplicationPermissions = Partial<Record<ApplicationResource, ApplicationAccess>>;

export interface ApplicationKey {
  id: number;
  /** Public part of the key ("mda_ab12cd34"), the secret is never shown again */
  identifier: string;
  description: string;
  permissions: ApplicationPermissions;
  /** IPs or CIDR ranges the key may be used from, empty = everywhere */
  allowedIps: string[];
  createdById?: number | null;
  createdByUsername?: string | null;
  createdAt: string;
  lastUsedAt?: string | null;
}

export interface ApplicationKeyRequest {
  description: string;
  permissions: ApplicationPermissions;
  /** Max 32 entries, each an IPv4/IPv6 address or CIDR range */
  allowedIps: string[];
}

/** Returned once on creation, the full token can't be read again */
export interface CreatedApplicationKey {
  key: ApplicationKey;
  /** "mda_{identifier}_{secret}" */
  token: string;
}

const base = "/api/admin/application-keys";

export const applicationKeysApi = {
  list: () => api.get<ApplicationKey[]>(base),
  create: (data: ApplicationKeyRequest) => api.post<CreatedApplicationKey>(base, data),
  update: (id: number, data: ApplicationKeyRequest) => api.put<ApplicationKey>(`${base}/${id}`, data),
  delete: (id: number) => api.delete(`${base}/${id}`),
};
