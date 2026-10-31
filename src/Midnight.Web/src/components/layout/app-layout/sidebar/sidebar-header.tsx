import { SidebarHeader } from "@/components/ui/sidebar";
import { useBranding } from "@/contexts/BrandingContext";
import { Link } from "react-router";

interface AppSidebarHeaderProps {
  showContent: boolean;
}

export default function AppSidebarHeader({ showContent }: AppSidebarHeaderProps) {
  const { branding } = useBranding();

  const logoLight = branding?.logoLightUrl;
  const logoDark = branding?.logoDarkUrl;
  const panelName = branding?.panelName || "Midnight Panel";

  return (
    <SidebarHeader className="px-5 py-6">
      <Link to="/" className="flex items-center gap-2.5 overflow-hidden group">
        {showContent ? (
          <>
            {logoLight && (
              <img
                className="h-7 w-auto max-w-[170px] object-contain dark:hidden transition-transform duration-200 group-hover:scale-102"
                src={logoLight}
                alt={panelName}
              />
            )}
            {logoDark && (
              <img
                className="hidden h-7 w-auto max-w-[170px] object-contain dark:block transition-transform duration-200 group-hover:scale-102"
                src={logoDark}
                alt={panelName}
              />
            )}
            {!logoLight && !logoDark && (
              <div className="flex items-center gap-2.5 overflow-hidden">
                <img
                  src="/images/logo/logo-icon-dark.png"
                  alt={panelName}
                  className="size-7 object-contain shrink-0"
                  onError={(e) => {
                    e.currentTarget.style.display = "none";
                  }}
                />
                <span className="font-outfit font-bold text-base text-gray-900 dark:text-white truncate tracking-tight">
                  {panelName}
                </span>
              </div>
            )}
          </>
        ) : (
          <img
            src="/images/logo/logo-icon-dark.png"
            alt={panelName}
            className="size-8 object-contain shrink-0"
          />
        )}
      </Link>
    </SidebarHeader>
  );
}
