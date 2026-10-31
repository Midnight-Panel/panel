import { SidebarHeader } from "@/components/ui/sidebar";
import { Link } from "react-router";

interface AppSidebarHeaderProps {
  showContent: boolean;
}

export default function AppSidebarHeader({ showContent }: AppSidebarHeaderProps) {
  return (
    <SidebarHeader className="px-5 py-6">
      <Link to="/">
        {showContent ? (
          <>
            <img className="h-7 w-auto dark:hidden" src="/images/logo/logo.png" alt="Midnight" />
            <img className="hidden h-7 w-auto dark:block" src="/images/logo/logo-dark.png" alt="Midnight" />
          </>
        ) : (
          <>
            <img src="/images/logo/logo-icon.png" alt="Midnight" className="size-8 dark:hidden" />
            <img src="/images/logo/logo-icon-dark.png" alt="Midnight" className="hidden size-8 dark:block" />
          </>
        )}
      </Link>
    </SidebarHeader>
  );
}
