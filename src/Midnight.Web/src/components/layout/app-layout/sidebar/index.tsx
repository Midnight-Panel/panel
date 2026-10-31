import { Sidebar, SidebarContent, SidebarFooter, useSidebar } from "@/components/ui/sidebar";
import { useAuth } from "@/auth/AuthContext";
import { useT } from "@/i18n";
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
  const { t } = useT();

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
          <div className="rounded-2xl bg-gray-50 px-4 py-4 text-center dark:bg-white/3">
            <p className="text-sm font-medium text-gray-800 dark:text-white/90">Midnight Panel</p>
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              v{settings.version} ·{" "}
              <a href="/api/reference" target="_blank" className="text-brand-500 hover:text-brand-600 dark:text-brand-400">
                {t("Footer.Api")}
              </a>
            </p>
          </div>
        </SidebarFooter>
      )}
    </Sidebar>
  );
}
