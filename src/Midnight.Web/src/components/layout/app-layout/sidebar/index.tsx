import { serversApi } from "@/api/servers";
import { Sidebar, SidebarContent, SidebarFooter, useSidebar } from "@/components/ui/sidebar";
import { useAuth } from "@/auth/AuthContext";
import { hasServerPermission } from "@/lib/server-permissions";
import { serverQueryKey } from "@/pages/servers/hooks";
import { serverNavigation, serverUuidFromPath } from "@/pages/servers/navigation";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useLocation } from "react-router";
import { adminItems, navItems } from "./data";
import AppSidebarHeader from "./sidebar-header";
import SidebarNavGroup from "./sidebar-nav-group";
import type { MenuType, NavItem } from "./types";

interface MenuSection {
  type: MenuType;
  labelKey: string;
  label?: string;
  items: NavItem[];
}

/** Pages of the server that is open right now, only those the user may see */
function useServerSection(pathname: string): MenuSection | null {
  const uuid = serverUuidFromPath(pathname);

  // Same query as the server layout, so this reuses its cached response
  const server = useQuery({
    queryKey: serverQueryKey(uuid ?? ""),
    queryFn: () => serversApi.get(uuid!),
    enabled: !!uuid,
  });

  if (!uuid || !server.data) return null;

  const items = serverNavigation(uuid)
    .filter((entry) => !entry.permission || hasServerPermission(server.data.permissions, entry.permission))
    .map<NavItem>((entry, index) => ({
      key: entry.key,
      icon: <span className="flex [&>svg]:size-6">{entry.icon}</span>,
      path: entry.path,
      // The console is the server's index page, every other page also matches its sub pages
      matchPrefix: index === 0 ? undefined : entry.path,
    }));

  return { type: "server", labelKey: "Nav.Section.Server", label: server.data.name, items };
}

export default function AppSidebar() {
  const { open, isMobile, isHovered } = useSidebar();
  const { pathname } = useLocation();
  const { can, settings } = useAuth();

  const showContent = open || isHovered || isMobile;

  const [openSubmenu, setOpenSubmenu] = useState<{ type: MenuType; index: number } | null>(null);

  const handleSubmenuToggle = (index: number, menuType: MenuType) => {
    setOpenSubmenu((previous) =>
      previous && previous.type === menuType && previous.index === index ? null : { type: menuType, index },
    );
  };

  const serverSection = useServerSection(pathname);

  const menuSections: MenuSection[] = [
    { type: "main", labelKey: "Nav.Section.Main", items: navItems },
    ...(serverSection ? [serverSection] : []),
    { type: "admin", labelKey: "Nav.Section.Admin", items: adminItems },
  ];

  const activeSections = menuSections
    .map((section) => ({ ...section, items: section.items.filter((item) => !item.permission || can(item.permission)) }))
    .filter((section) => section.items.length > 0);

  return (
    <Sidebar side="left" collapsible="icon">
      <AppSidebarHeader showContent={showContent} />

      <SidebarContent className="overflow-y-auto px-4 duration-300 ease-linear">
        <nav className="mb-6">
          <div className="flex flex-col gap-4">
            {activeSections.map((section) => (
              <SidebarNavGroup
                key={section.type}
                labelKey={section.labelKey}
                label={section.label}
                menuType={section.type}
                items={section.items}
                openSubmenu={openSubmenu}
                showContent={showContent}
                pathname={pathname}
                onToggleSubmenu={handleSubmenuToggle}
              />
            ))}
          </div>
        </nav>
      </SidebarContent>

      {showContent && (
        <SidebarFooter className="p-0 px-4 pt-1 pb-4">
          <div className="rounded-xl border border-gray-200/70 bg-gray-50/60 px-3 py-2 text-center dark:border-white/5 dark:bg-white/[0.02]">
            <div className="flex items-center justify-between text-xs text-gray-500 dark:text-gray-400">
              <span className="flex items-center gap-1.5 font-mono text-[11px]">
                <span className="size-1.5 rounded-full bg-emerald-500 animate-pulse" />
                v{settings.version || "0.1.0"}
              </span>
              <span className="text-[11px] font-medium text-emerald-500">
                Online
              </span>
            </div>
          </div>
        </SidebarFooter>
      )}
    </Sidebar>
  );
}
