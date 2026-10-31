import { Sidebar, SidebarContent, SidebarFooter, useSidebar } from "@/components/ui/sidebar";
import { useAuth } from "@/auth/AuthContext";
import { useState } from "react";
import { useLocation } from "react-router";
import { adminItems, navItems } from "./data";
import AppSidebarHeader from "./sidebar-header";
import SidebarNavGroup from "./sidebar-nav-group";
import type { MenuType, NavItem } from "./types";

const menuSections: { type: MenuType; labelKey: string; items: NavItem[] }[] = [
  { type: "main", labelKey: "Nav.Section.Main", items: navItems },
  { type: "admin", labelKey: "Nav.Section.Admin", items: adminItems },
];

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
