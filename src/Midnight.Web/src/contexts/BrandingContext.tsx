import React, { createContext, useContext, useEffect, useState, useCallback } from "react";
import { brandingApi } from "@/api/branding";
import type { BrandingResponse } from "@/types/branding";

interface BrandingContextType {
  branding: BrandingResponse | null;
  loading: boolean;
  reload: () => Promise<void>;
}

const defaultPoweredBy = {
  text: "Powered by Midnight Panel",
  url: "https://midnightpanel.com",
  enforced: true,
};

const BrandingContext = createContext<BrandingContextType>({
  branding: null,
  loading: true,
  reload: async () => {},
});

export const BrandingProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [branding, setBranding] = useState<BrandingResponse | null>(null);
  const [loading, setLoading] = useState(true);

  const applyBranding = useCallback((data: BrandingResponse) => {
    const root = document.documentElement;

    if (data.primaryColor) {
      root.style.setProperty("--mp-primary", data.primaryColor);
    }
    if (data.accentColor) {
      root.style.setProperty("--mp-accent", data.accentColor);
    }

    if (data.panelName) {
      document.title = data.panelName;
    }

    if (data.faviconUrl) {
      let link: HTMLLinkElement | null = document.querySelector("link[rel*='icon']");
      if (!link) {
        link = document.createElement("link");
        link.rel = "shortcut icon";
        document.head.appendChild(link);
      }
      link.href = data.faviconUrl;
    }

    let styleTag = document.getElementById("mp-custom-css") as HTMLStyleElement | null;
    if (data.customCss) {
      if (!styleTag) {
        styleTag = document.createElement("style");
        styleTag.id = "mp-custom-css";
        document.head.appendChild(styleTag);
      }
      styleTag.textContent = data.customCss;
    } else if (styleTag) {
      styleTag.remove();
    }
  }, []);

  const load = useCallback(async (fresh = false) => {
    try {
      const res = await brandingApi.get(fresh);
      const validated: BrandingResponse = {
        ...res,
        poweredBy: res.poweredBy || defaultPoweredBy,
      };
      setBranding(validated);
      applyBranding(validated);
    } catch (err) {
      console.error("Failed to load branding:", err);
    } finally {
      setLoading(false);
    }
  }, [applyBranding]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <BrandingContext.Provider value={{ branding, loading, reload: () => load(true) }}>
      {children}
    </BrandingContext.Provider>
  );
};

export const useBranding = () => useContext(BrandingContext);
