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

export interface FooterLinkItem {
  id: string;
  label: string;
  url: string;
  newTab: boolean;
}

export interface PublicSettings {
  appName: string;
  version: string;
  registrationEnabled: boolean;
  setupRequired: boolean;
  userApiEnabled?: boolean;
  maintenanceMode?: boolean;
  footerLinks?: FooterLinkItem[];
}

export interface AdminSettings {
  appName: string;
  publicUrl: string;
  enableRegistration: boolean;
  userApiEnabled: boolean;
  maintenanceMode: boolean;
  serverCreationEnabled: boolean;
  telemetryEnabled: boolean;
  footerLinks: FooterLinkItem[];
}

export interface AdminSettingsUpdateRequest {
  appName: string;
  publicUrl: string;
  enableRegistration: boolean;
  userApiEnabled: boolean;
  maintenanceMode: boolean;
  serverCreationEnabled: boolean;
  telemetryEnabled: boolean;
  footerLinks: FooterLinkItem[];
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
  demonVersion: string;
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

export interface DemonLogs {
  lines: string[];
}

// Shards & Shard Collections

export interface ShardCollection {
  id: number;
  uuid: string;
  name: string;
  description?: string | null;
  author: string;
  shardCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface ShardCollectionRequest {
  name: string;
  description?: string | null;
  author: string;
}

export interface ShardSummary {
  id: number;
  uuid: string;
  collectionId: number;
  collectionName: string;
  name: string;
  description?: string | null;
  author: string;
  version: string;
  defaultDockerImage: string;
  variableCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface ShardConfigFile {
  path: string;
  parser: "properties" | "yaml" | "json" | "ini" | "xml" | string;
  mappings: Record<string, string>;
}

export interface ShardFeatures {
  eula: boolean;
  pidLimit?: number | null;
  fileDenylist: string[];
}

export interface ShardVariable {
  id: number;
  shardId: number;
  name: string;
  description?: string | null;
  envVariable: string;
  defaultValue?: string | null;
  userViewable: boolean;
  userEditable: boolean;
  rules: string;
  sortOrder: number;
}

export interface ShardVariableRequest {
  name: string;
  description?: string | null;
  envVariable: string;
  defaultValue?: string | null;
  userViewable: boolean;
  userEditable: boolean;
  rules: string;
  sortOrder: number;
}

export interface ShardDetail {
  id: number;
  uuid: string;
  collectionId: number;
  collectionName: string;
  name: string;
  description?: string | null;
  author: string;
  version: string;
  dockerImages: Record<string, string>;
  defaultDockerImage: string;
  startupCommand: string;
  stopCommand?: string | null;
  stopSignal: string;
  configFiles: ShardConfigFile[];
  startupDone: string;
  installScript?: string | null;
  installImage: string;
  installInterpreter: string;
  features: ShardFeatures;
  variables: ShardVariable[];
  createdAt: string;
  updatedAt: string;
}

export interface ShardRequest {
  collectionId: number;
  name: string;
  description?: string | null;
  author: string;
  version: string;
  dockerImages: Record<string, string>;
  defaultDockerImage: string;
  startupCommand: string;
  stopCommand?: string | null;
  stopSignal: string;
  configFiles: ShardConfigFile[];
  startupDone: string;
  installScript?: string | null;
  installImage: string;
  installInterpreter: string;
  features: ShardFeatures;
}

export interface ShardExportPackage {
  formatVersion: string;
  exportedAt: string;
  collection: {
    name: string;
    description?: string | null;
    author: string;
  };
  shard: {
    name: string;
    description?: string | null;
    author: string;
    version: string;
    dockerImages: Record<string, string>;
    defaultDockerImage: string;
    startupCommand: string;
    stopCommand?: string | null;
    stopSignal: string;
    configFiles: ShardConfigFile[];
    startupDone: string;
    installScript?: string | null;
    installImage: string;
    installInterpreter: string;
    features: ShardFeatures;
    variables: Array<{
      name: string;
      description?: string | null;
      envVariable: string;
      defaultValue?: string | null;
      userViewable: boolean;
      userEditable: boolean;
      rules: string;
      sortOrder: number;
    }>;
  };
}

export interface ShardImportRequest {
  targetCollectionId?: number | null;
  content: string;
}

export interface ShardImportResult {
  shardId: number;
  shardUuid: string;
  shardName: string;
  collectionId: number;
  collectionName: string;
  detectedFormat: string;
  warnings: string[];
}

export interface ValidateVariablesRequest {
  values: Record<string, string | null>;
}

export interface ValidateVariablesResponse {
  isValid: boolean;
  errors: Record<string, string>;
  resolvedEnvironment: Record<string, string>;
  resolvedStartupCommand?: string | null;
}

// Extensions

export interface ExtensionSummary {
  id: number;
  identifier: string;
  name: string;
  description: string;
  version: string;
  author: string;
  category: string;
  websiteUrl?: string | null;
  icon?: string | null;
  isEnabled: boolean;
  installedAt: string;
  updatedAt: string;
}

export interface ExtensionDetail extends ExtensionSummary {
  configSchemaJson?: string | null;
  configValuesJson?: string | null;
  readmeMarkdown?: string | null;
  requiredPermissions: string[];
}

export interface ExtensionInstallRequest {
  identifier: string;
  name: string;
  description: string;
  version?: string;
  author?: string;
  category?: string;
  websiteUrl?: string | null;
  icon?: string | null;
  configSchemaJson?: string | null;
  configValuesJson?: string | null;
  readmeMarkdown?: string | null;
}

export interface ExtensionConfigUpdateRequest {
  configValuesJson: string;
}

export interface ExtensionToggleRequest {
  isEnabled: boolean;
}


// Servers (Midnight.Shared/Http/Servers, Enums, Protocol/ConsoleProtocol)

/** Live container state, from the demon */
export type ServerState = "Offline" | "Starting" | "Online" | "Stopping" | "Installing";

/** Lifecycle of a server in the panel */
export type ServerStatus = "Installing" | "InstallFailed" | "Ready";

export type PowerAction = "Start" | "Stop" | "Restart" | "Kill";

export interface ServerLimits {
  /** Percent of one core (100 = 1 core), 0 = unlimited */
  cpu: number;
  /** MiB */
  memory: number;
  /** MiB, 0 = disabled, -1 = unlimited */
  swap: number;
  /** MiB, 0 = unlimited */
  disk: number;
  /** 10-1000 */
  ioWeight: number;
  /** e.g. "0", "0-3" or "0,2,4", empty = all cores */
  threads?: string | null;
  oomKillerDisabled: boolean;
}

export interface ServerFeatureLimits {
  databases: number;
  allocations: number;
  backups: number;
}

export interface ServerAllocation {
  id: number;
  ipAddress: string;
  port: number;
  notes?: string | null;
  isPrimary: boolean;
}

export interface ServerVariable {
  shardVariableId: number;
  name: string;
  description?: string | null;
  envVariable: string;
  defaultValue?: string | null;
  /** null = the default */
  value?: string | null;
  userViewable: boolean;
  userEditable: boolean;
  rules: string;
}

export interface AdminServerSummary {
  id: number;
  uuid: string;
  name: string;
  ownerId: number;
  ownerUsername: string;
  nodeId: number;
  nodeName: string;
  shardId: number;
  shardName: string;
  status: ServerStatus;
  suspended: boolean;
  /** "ip:port" */
  primaryAllocation?: string | null;
  limits: ServerLimits;
  createdAt: string;
}

export interface AdminServerDetail extends AdminServerSummary {
  description?: string | null;
  ownerEmail: string;
  dockerImage: string;
  /** null = the shard's command */
  startupCommand?: string | null;
  shardStartupCommand: string;
  /** Label => image */
  shardDockerImages: Record<string, string>;
  featureLimits: ServerFeatureLimits;
  allocations: ServerAllocation[];
  variables: ServerVariable[];
  installedAt?: string | null;
  updatedAt: string;
}

export interface CreateServerRequest {
  name: string;
  description?: string | null;
  ownerId: number;
  nodeId: number;
  shardId: number;
  dockerImage?: string | null;
  startupCommand?: string | null;
  limits: ServerLimits;
  featureLimits: ServerFeatureLimits;
  primaryAllocationId: number;
  additionalAllocationIds: number[];
  /** EnvVariable => value */
  variables: Record<string, string>;
  startOnCompletion: boolean;
  skipInstall: boolean;
}

export interface UpdateServerDetailsRequest {
  name: string;
  description?: string | null;
  ownerId: number;
}

export interface UpdateServerBuildRequest {
  limits: ServerLimits;
  featureLimits: ServerFeatureLimits;
  primaryAllocationId: number;
  additionalAllocationIds: number[];
}

export interface UpdateServerStartupRequest {
  shardId: number;
  dockerImage: string;
  startupCommand?: string | null;
  variables: Record<string, string>;
}

export interface ServerSummary {
  uuid: string;
  name: string;
  description?: string | null;
  nodeName: string;
  shardName: string;
  status: ServerStatus;
  suspended: boolean;
  primaryAllocation?: string | null;
  limits: ServerLimits;
  isOwner: boolean;
}

export interface ServerDetail extends ServerSummary {
  ownerUsername: string;
  nodeFqdn: string;
  sftpPort: number;
  dockerImage: string;
  startupPreview: string;
  featureLimits: ServerFeatureLimits;
  allocations: ServerAllocation[];
  /** Only the variables the user may see */
  variables: ServerVariable[];
  /** Server permissions of the current user, ["*"] for the owner and admins (see lib/server-permissions) */
  permissions: string[];
}

export interface ServerStats {
  /** Percent of one core (200 = two cores) */
  cpuPercent: number;
  memoryBytes: number;
  memoryLimitBytes: number;
  diskBytes: number;
  networkRxBytes: number;
  networkTxBytes: number;
  /** 0 when offline */
  uptimeSeconds: number;
}

export interface ServerResources {
  state: ServerState;
  /** False when the node could not be reached */
  nodeOnline: boolean;
  stats: ServerStats;
}

export interface PowerRequest {
  action: PowerAction;
}

export interface CommandRequest {
  command: string;
}

export interface UpdateServerVariablesRequest {
  variables: Record<string, string>;
}

export interface RenameServerRequest {
  name: string;
  description?: string | null;
}

export interface ConsoleTicket {
  ticket: string;
  /** Relative websocket path: /api/servers/{uuid}/console?ticket=... */
  path: string;
}

/** Websocket messages of the console, {"event": "...", "data": ...} */
export type ConsoleServerMessage =
  | { event: "history"; data: string[] }
  | { event: "console"; data: string }
  | { event: "install-output"; data: string }
  | { event: "install-started"; data: null }
  | { event: "install-completed"; data: boolean | null }
  | { event: "status"; data: ServerState }
  | { event: "stats"; data: ServerStats }
  | { event: "notice"; data: string }
  | { event: "error"; data: string }
  | { event: "backup-completed"; data: BackupCompletedEvent }
  | { event: "backup-restore-started"; data: null }
  | { event: "backup-restore-completed"; data: { successful: boolean } | null };

export interface BackupCompletedEvent {
  uuid: string;
  successful: boolean;
  bytes: number;
}

export type ConsoleClientMessage =
  | { event: "command"; data: string }
  | { event: "power"; data: PowerAction };
