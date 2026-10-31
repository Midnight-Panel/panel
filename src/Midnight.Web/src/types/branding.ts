import type { FooterLinkItem } from "@/api/types";

export interface PoweredByInfo {
  readonly text: string;
  readonly url: string;
  readonly enforced: boolean;
}

export interface BrandingResponse {
  panelName: string;
  slogan?: string | null;
  panelUrl?: string | null;
  defaultLanguage?: string | null;
  timeZone?: string | null;
  footerLinks?: FooterLinkItem[];
  logoLightUrl: string | null;
  logoDarkUrl: string | null;
  faviconUrl: string | null;
  primaryColor: string;
  accentColor: string;
  loginBackgroundType: "none" | "gradient" | "image";
  loginBackgroundValue: string | null;
  customCss: string | null;
  poweredBy: PoweredByInfo;
  updatedAt: string;
}

export interface BrandingUpdateRequest {
  panelName: string;
  slogan?: string | null;
  panelUrl?: string | null;
  defaultLanguage?: string | null;
  timeZone?: string | null;
  footerLinks?: FooterLinkItem[];
  primaryColor: string;
  accentColor: string;
  loginBackgroundType: "none" | "gradient" | "image";
  loginBackgroundValue?: string | null;
  customCss?: string | null;
}
