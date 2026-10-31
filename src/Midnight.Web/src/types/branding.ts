export interface PoweredByInfo {
  readonly text: string;
  readonly url: string;
  readonly enforced: boolean;
}

export interface BrandingResponse {
  panelName: string;
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
  primaryColor: string;
  accentColor: string;
  loginBackgroundType: "none" | "gradient" | "image";
  loginBackgroundValue?: string | null;
  customCss?: string | null;
}
