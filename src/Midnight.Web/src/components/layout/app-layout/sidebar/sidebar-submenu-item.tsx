import { useT } from "@/i18n";
import { cn } from "@/utils";
import { Link } from "react-router";
import type { SubItem } from "./types";

interface SidebarSubmenuItemProps {
  subItem: SubItem;
  isActive: boolean;
}

export default function SidebarSubmenuItem({ subItem, isActive }: SidebarSubmenuItemProps) {
  const { t } = useT();

  return (
    <li>
      <Link
        to={subItem.path}
        className={cn("group menu-dropdown-item", isActive ? "menu-dropdown-item-active" : "menu-dropdown-item-inactive")}
      >
        {t(subItem.key)}
      </Link>
    </li>
  );
}
