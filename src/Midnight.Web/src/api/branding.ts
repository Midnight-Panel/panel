import { api } from "./client";
import type { BrandingResponse, BrandingUpdateRequest } from "../types/branding";

export const brandingApi = {
  get: () => api.get<BrandingResponse>("/api/branding"),

  update: (data: BrandingUpdateRequest) =>
    api.put<BrandingResponse>("/api/admin/branding", data),

  uploadLogo: (theme: "light" | "dark", file: File) => {
    const formData = new FormData();
    formData.append("file", file, file.name);
    return api.post<BrandingResponse>(`/api/admin/branding/logo?theme=${theme}`, formData);
  },

  uploadFavicon: (file: File) => {
    const formData = new FormData();
    formData.append("file", file, file.name);
    return api.post<BrandingResponse>("/api/admin/branding/favicon", formData);
  },

  reset: () => api.post<BrandingResponse>("/api/admin/branding/reset"),
};
