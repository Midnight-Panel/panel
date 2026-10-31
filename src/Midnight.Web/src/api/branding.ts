import { api } from "./client";
import type { BrandingResponse, BrandingUpdateRequest } from "../types/branding";

export const brandingApi = {
  /** fresh skips the browser cache (the endpoint is cached for anonymous visitors), used after changes */
  get: (fresh = false) => api.get<BrandingResponse>(fresh ? `/api/branding?v=${Date.now()}` : "/api/branding"),

  update: (data: BrandingUpdateRequest) =>
    api.put<BrandingResponse>("/api/admin/branding", data),

  uploadLogo: (theme: "light" | "dark", file: File) => {
    const formData = new FormData();
    formData.append("file", file, file.name);
    return api.post<BrandingResponse>(`/api/admin/branding/logo?theme=${theme}`, formData);
  },

  deleteLogo: (theme: "light" | "dark") =>
    api.delete(`/api/admin/branding/logo?theme=${theme}`),

  uploadFavicon: (file: File) => {
    const formData = new FormData();
    formData.append("file", file, file.name);
    return api.post<BrandingResponse>("/api/admin/branding/favicon", formData);
  },

  deleteFavicon: () =>
    api.delete("/api/admin/branding/favicon"),

  reset: () => api.post<BrandingResponse>("/api/admin/branding/reset"),
};
