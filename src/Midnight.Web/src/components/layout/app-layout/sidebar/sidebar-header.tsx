import { SidebarHeader } from "@/components/ui/sidebar";
import { useBranding } from "@/contexts/BrandingContext";
import { Link } from "react-router";

interface AppSidebarHeaderProps {
  showContent: boolean;
}

export default function AppSidebarHeader({ showContent }: AppSidebarHeaderProps) {
  const { branding } = useBranding();

  const logoLight = branding?.logoLightUrl || "/images/logo/logo.png";
  const logoDark = branding?.logoDarkUrl || "/images/logo/logo-dark.png";
  const panelName = branding?.panelName || "Midnight Panel";

  return (
    <SidebarHeader className="px-5 py-6">
      <Link to="/" className="flex items-center gap-2">
        {showContent ? (
          <>
            <img
              className="h-7 w-auto object-contain dark:hidden"
              src={logoLight}
              alt={panelName}
              onError={(e) => {
                e.currentTarget.src = "/images/logo/logo.png";
              }}
            />
            <img
              className="hidden h-7 w-auto object-contain dark:block"
              src={logoDark}
              alt={panelName}
              onError={(e) => {
                e.currentTarget.src = "/images/logo/logo-dark.png";
              }}
            />
          </>
        ) : (
          <>
            <img
              src="/images/logo/logo-icon.png"
              alt={panelName}
              className="size-8 object-contain dark:hidden"
            />
            <img
              src="/images/logo/logo-icon-dark.png"
              alt={panelName}
              className="hidden size-8 object-contain dark:block"
            />
          </>
        )}
      </Link>
    </SidebarHeader>
  );
}
