import { BoxIcon, BrushIcon, GearIcon, GroupIcon, LayerIcon, LayoutGridIcon, PieChartIcon } from "@/icons";
import { Permissions } from "@/lib/permissions";
import type { NavItem } from "./types";

// TODO: Phase 7 - modules and plugins register their entries here
export const navItems: NavItem[] = [
  {
    icon: <LayoutGridIcon className="size-6" />,
    key: "Nav.Dashboard",
    path: "/",
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
    icon: <BoxIcon className="size-6" />,
    key: "Nav.Admin.Shards",
    path: "/admin/shards",
    matchPrefix: "/admin/shards",
    permission: Permissions.AdminShardsRead,
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
