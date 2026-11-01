import { ApplicationResources, type ApplicationAccess, type ApplicationPermissions, type ApplicationResource } from "@/api/application-keys";
import { Permissions } from "@/lib/permissions";

export const applicationKeysQueryKey = ["admin", "application-keys"] as const;

/** Admin permissions needed to grant Read / ReadWrite on a resource (mirrors the api rules) */
export const resourceRequirements: Record<ApplicationResource, { read: string; write: string }> = {
  Users: { read: Permissions.AdminUsersRead, write: Permissions.AdminUsersWrite },
  Servers: { read: Permissions.AdminServersRead, write: Permissions.AdminServersWrite },
  Nodes: { read: Permissions.AdminNodesRead, write: Permissions.AdminNodesWrite },
  Allocations: { read: Permissions.AdminNodesRead, write: Permissions.AdminNodesWrite },
  Shards: { read: Permissions.AdminShardsRead, write: Permissions.AdminShardsWrite },
  DatabaseHosts: { read: Permissions.AdminServersRead, write: Permissions.AdminServersWrite },
};

export function canGrant(resource: ApplicationResource, access: ApplicationAccess, can: (permission: string) => boolean) {
  if (access === "None") return true;
  const requirement = resourceRequirements[resource];
  return can(access === "Read" ? requirement.read : requirement.write);
}

export type FullPermissions = Record<ApplicationResource, ApplicationAccess>;

/** Responses contain every resource, but missing ones count as None */
export function fullPermissions(permissions: ApplicationPermissions | undefined): FullPermissions {
  return Object.fromEntries(ApplicationResources.map((resource) => [resource, permissions?.[resource] ?? "None"])) as FullPermissions;
}

function isIPv4(value: string) {
  const parts = value.split(".");
  return parts.length === 4 && parts.every((part) => /^\d{1,3}$/.test(part) && Number(part) <= 255 && (part === "0" || !part.startsWith("0")));
}

function isIPv6(value: string) {
  if (!/^[0-9a-fA-F:.]+$/.test(value)) return false;

  const halves = value.split("::");
  if (halves.length > 2) return false;

  const groups = (half: string) => (half === "" ? [] : half.split(":"));
  const head = groups(halves[0]);
  const tail = halves.length === 2 ? groups(halves[1]) : [];
  const all = [...head, ...tail];

  // An embedded IPv4 address (::ffff:1.2.3.4) counts as two groups and must be last
  let count = all.length;
  const last = all.at(-1);
  if (last?.includes(".")) {
    if (!isIPv4(last)) return false;
    all.pop();
    count++;
  }

  if (!all.every((group) => /^[0-9a-fA-F]{1,4}$/.test(group))) return false;

  return halves.length === 2 ? count < 8 : count === 8;
}

/** IPv4 / IPv6 address or CIDR range, the api validates again */
export function isIpOrCidr(value: string) {
  const [address, prefix, ...rest] = value.split("/");
  if (rest.length > 0) return false;

  const v4 = isIPv4(address);
  if (!v4 && !isIPv6(address)) return false;
  if (prefix === undefined) return true;

  return /^\d{1,3}$/.test(prefix) && Number(prefix) <= (v4 ? 32 : 128);
}

export function parseIpLines(value: string) {
  return value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
}
