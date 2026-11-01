// Mirror of Midnight.Shared.Permissions. The api enforces permissions, the ui only hides what isn't allowed.

export const Permissions = {
  Everything: "*",
  AccountRead: "account.read",
  AccountWrite: "account.write",
  AccountSecurity: "account.security",
  AdminOverview: "admin.overview",
  AdminUsersRead: "admin.users.read",
  AdminUsersWrite: "admin.users.write",
  AdminUsersImpersonate: "admin.users.impersonate",
  AdminRolesRead: "admin.roles.read",
  AdminRolesWrite: "admin.roles.write",
  AdminSystemRead: "admin.system.read",
  AdminSystemWrite: "admin.system.write",
  AdminNodesRead: "admin.nodes.read",
  AdminNodesWrite: "admin.nodes.write",
  AdminShardsRead: "admin.shards.read",
  AdminShardsWrite: "admin.shards.write",
  AdminExtensionsRead: "admin.extensions.read",
  AdminExtensionsWrite: "admin.extensions.write",
  AdminImagesRead: "admin.images.read",
  AdminImagesWrite: "admin.images.write",
  AdminServersRead: "admin.servers.read",
  AdminServersWrite: "admin.servers.write",
  AdminApiRead: "admin.api.read",
  AdminApiWrite: "admin.api.write"
} as const;

export interface PermissionGroup {
  name: string;
  permissions: string[];
}

export const PermissionGroups: PermissionGroup[] = [
  { name: "account", permissions: [Permissions.AccountRead, Permissions.AccountWrite, Permissions.AccountSecurity] },
  { name: "admin.overview", permissions: [Permissions.AdminOverview] },
  { name: "admin.users", permissions: [Permissions.AdminUsersRead, Permissions.AdminUsersWrite, Permissions.AdminUsersImpersonate] },
  { name: "admin.roles", permissions: [Permissions.AdminRolesRead, Permissions.AdminRolesWrite] },
  { name: "admin.system", permissions: [Permissions.AdminSystemRead, Permissions.AdminSystemWrite] },
  { name: "admin.nodes", permissions: [Permissions.AdminNodesRead, Permissions.AdminNodesWrite] },
  { name: "admin.shards", permissions: [Permissions.AdminShardsRead, Permissions.AdminShardsWrite] },
  { name: "admin.extensions", permissions: [Permissions.AdminExtensionsRead, Permissions.AdminExtensionsWrite] },
  { name: "admin.images", permissions: [Permissions.AdminImagesRead, Permissions.AdminImagesWrite] },
  { name: "admin.servers", permissions: [Permissions.AdminServersRead, Permissions.AdminServersWrite] },
  { name: "admin.api", permissions: [Permissions.AdminApiRead, Permissions.AdminApiWrite] }
];

export function matchesPermission(granted: string, required: string): boolean {
  if (!granted) return false;
  if (granted === Permissions.Everything) return true;

  if (granted.endsWith(".*")) {
    const prefix = granted.slice(0, -2);
    return required === prefix || required.startsWith(prefix + ".");
  }

  return granted === required;
}

export function hasPermission(granted: readonly string[], required: string): boolean {
  return granted.some((x) => matchesPermission(x, required));
}
