// TypeScript mirror of the DTOs in Midnight.Shared (camelCase json, dates as ISO strings)

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PagedQuery {
  page: number;
  pageSize: number;
  search?: string;
  sortBy?: string;
  sortDescending?: boolean;
}

// Auth

export interface LoginRequest {
  email: string;
  password: string;
  twoFactorCode?: string | null;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
}

export interface LoginResponse {
  requiresTwoFactor: boolean;
  accessToken?: string | null;
  expiresAt?: string | null;
}

export interface PublicSettings {
  appName: string;
  version: string;
  registrationEnabled: boolean;
  setupRequired: boolean;
}

// Account

export interface CurrentUser {
  id: number;
  username: string;
  email: string;
  hasAvatar: boolean;
  twoFactorEnabled: boolean;
  permissions: string[];
  impersonatedBy?: string | null;
}

export interface TwoFactorSetup {
  secret: string;
  uri: string;
  qrCode: string;
}

export interface Session {
  id: number;
  ipAddress?: string | null;
  userAgent?: string | null;
  createdAt: string;
  lastUsedAt: string;
  isCurrent: boolean;
}

export interface ApiKey {
  id: number;
  name: string;
  prefix: string;
  scopes: string[];
  createdAt: string;
  expiresAt?: string | null;
  lastUsedAt?: string | null;
}

export interface CreateApiKeyResponse {
  apiKey: ApiKey;
  key: string;
}

// Admin

export interface AdminUser {
  id: number;
  username: string;
  email: string;
  hasAvatar: boolean;
  isLocked: boolean;
  twoFactorEnabled: boolean;
  roleIds: number[];
  roleNames: string[];
  createdAt: string;
  lastLoginAt?: string | null;
}

export interface UpdateUserRequest {
  username: string;
  email: string;
  password?: string | null;
  isLocked: boolean;
  roleIds: number[];
}

export interface Role {
  id: number;
  name: string;
  description?: string | null;
  permissions: string[];
  userCount: number;
}

export interface RoleRequest {
  name: string;
  description?: string | null;
  permissions: string[];
}

export interface AdminSession {
  id: number;
  userId: number;
  username: string;
  email: string;
  ipAddress?: string | null;
  userAgent?: string | null;
  createdAt: string;
  lastUsedAt: string;
}

export interface ImpersonationResponse {
  accessToken: string;
  expiresAt: string;
}

export interface AdminOverview {
  userCount: number;
  lockedUserCount: number;
  serverCount: number;
  nodeCount: number;
  version: string;
  uptime: string;
  memoryUsageBytes: number;
  operatingSystem: string;
  runtime: string;
}

// Nodes

export interface NodeItem {
  id: number;
  name: string;
  fqdn: string;
  httpPort: number;
  sftpPort: number;
  useSsl: boolean;
  allocationCount: number;
  createdAt: string;
}

export interface NodeRequest {
  name: string;
  fqdn: string;
  httpPort: number;
  sftpPort: number;
  useSsl: boolean;
}

export interface DockerInfo {
  available: boolean;
  version?: string | null;
  error?: string | null;
  containers: number;
  containersRunning: number;
  images: number;
}

export interface SystemInfo {
  daemonVersion: string;
  protocolVersion: number;
  operatingSystem: string;
  kernelVersion: string;
  architecture: string;
  hostname: string;
  uptime: string;
  cpuCores: number;
  cpuModel?: string | null;
  cpuUsage: number;
  memoryTotalBytes: number;
  memoryUsedBytes: number;
  diskTotalBytes: number;
  diskUsedBytes: number;
  docker: DockerInfo;
}

export interface NodeStatus {
  online: boolean;
  error?: string | null;
  latencyMs?: number | null;
  system?: SystemInfo | null;
}

export interface NodeSetup {
  configuration: string;
  command: string;
}

export interface Allocation {
  id: number;
  ipAddress: string;
  port: number;
  notes?: string | null;
  serverId?: number | null;
}

export interface CreateAllocationsResponse {
  created: number;
  skipped: number;
}

export interface DaemonLogs {
  lines: string[];
}
