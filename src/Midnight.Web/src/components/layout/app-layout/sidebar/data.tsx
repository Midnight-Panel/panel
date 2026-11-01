import { BoxIcon, BrushIcon, ChipIcon, CodeIcon, DatabaseIcon, GearIcon, GroupIcon, LayerIcon, LayoutGridIcon, PieChartIcon, PluginIcon, UserCircleIcon } from "@/icons";
import { Permissions } from "@/lib/permissions";
import type { NavItem } from "./types";

// TODO: Phase 7 - modules and plugins register their entries here
export const navItems: NavItem[] = [
  {
    icon: <LayoutGridIcon className="size-6" />,
    key: "Nav.Dashboard",
    path: "/",
  },
  {
    icon: <ChipIcon className="size-6" />,
    key: "Nav.Servers",
    path: "/servers",
    matchPrefix: "/servers",
  },
  {
    icon: <UserCircleIcon className="size-6" />,
    key: "Nav.Account",
    subItems: [
      { key: "Account.Overview", path: "/account" },
      { key: "Nav.Security", path: "/account/security" },
      { key: "Nav.ApiKeys", path: "/account/api-keys" },
    ],
  },
];

export const adminItems: NavItem[] = [
  {
    icon: <PieChartIcon className="size-6" />,
    key: "Nav.Admin.Overview",
    path: "/admin",
    permission: Permissions.AdminOverview,
  },
  {
    icon: <GroupIcon className="size-6" />,
    key: "Nav.Admin.Users",
    path: "/admin/users",
    matchPrefix: "/admin/users",
    permission: Permissions.AdminUsersRead,
  },
  {
    icon: <LayerIcon className="size-6" />,
    key: "Nav.Admin.Nodes",
    path: "/admin/nodes",
    matchPrefix: "/admin/nodes",
    permission: Permissions.AdminNodesRead,
  },
  {
    icon: <ChipIcon className="size-6" />,
    key: "Nav.Admin.Servers",
    path: "/admin/servers",
    matchPrefix: "/admin/servers",
    permission: Permissions.AdminServersRead,
  },
  {
    icon: <DatabaseIcon className="size-6" />,
    key: "Nav.Admin.DatabaseHosts",
    path: "/admin/database-hosts",
    matchPrefix: "/admin/database-hosts",
    permission: Permissions.AdminServersRead,
  },
  {
    icon: <BoxIcon className="size-6" />,
    key: "Nav.Admin.Shards",
    path: "/admin/shards",
    matchPrefix: "/admin/shards",
    permission: Permissions.AdminShardsRead,
  },
  {
    icon: <PluginIcon className="size-6" />,
    key: "Nav.Admin.Extensions",
    path: "/admin/extensions",
    matchPrefix: "/admin/extensions",
    permission: Permissions.AdminExtensionsRead,
  },
  {
    icon: <CodeIcon className="size-6" />,
    key: "Nav.Admin.ApplicationApi",
    path: "/admin/api",
    matchPrefix: "/admin/api",
    permission: Permissions.AdminApiRead,
  },
  {
    icon: <BrushIcon className="size-6" />,
    key: "Nav.Admin.Customizer",
    path: "/admin/customizer",
    matchPrefix: "/admin/customizer",
    permission: Permissions.AdminSystemWrite,
  },
  {
    icon: <GearIcon className="size-6" />,
    key: "Nav.Admin.Settings",
    path: "/admin/settings",
    matchPrefix: "/admin/settings",
    permission: Permissions.AdminSystemRead,
  },
];
