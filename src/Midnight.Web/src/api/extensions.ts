import { api, toQueryString, tokens } from "./client";
import type {
  ExtensionConfigUpdateRequest,
  ExtensionDetail,
  ExtensionInstallRequest,
  ExtensionInstallUrlRequest,
  ExtensionSummary,
  ExtensionToggleRequest,
  PagedQuery,
  PagedResult,
} from "./types";

export const extensionsApi = {
  list: (query: PagedQuery, category?: string) => {
    const params = new URLSearchParams(toQueryString(query));
    if (category) {
      params.set("category", category);
    }
    return api.get<PagedResult<ExtensionSummary>>(`/api/admin/extensions?${params.toString()}`);
  },

  getCategories: () =>
    api.get<string[]>("/api/admin/extensions/categories"),

  get: (id: number) =>
    api.get<ExtensionDetail>(`/api/admin/extensions/${id}`),

  toggle: (id: number, isEnabled: boolean) =>
    api.put<ExtensionDetail>(`/api/admin/extensions/${id}/toggle`, { isEnabled } satisfies ExtensionToggleRequest),

  updateConfig: (id: number, configValuesJson: string) =>
    api.put<ExtensionDetail>(`/api/admin/extensions/${id}/config`, { configValuesJson } satisfies ExtensionConfigUpdateRequest),

  install: (data: ExtensionInstallRequest) =>
    api.post<ExtensionDetail>("/api/admin/extensions", data),

  uploadPackage: (file: File) => {
    const formData = new FormData();
    formData.append("file", file, file.name);
    return api.post<ExtensionDetail>("/api/admin/extensions/upload", formData);
  },

  installFromUrl: (url: string) =>
    api.post<ExtensionDetail>("/api/admin/extensions/url", { url } satisfies ExtensionInstallUrlRequest),

  downloadPackage: async (id: number, fallbackFileName = "extension.zip") => {
    const token = await tokens.getAccessToken();
    const response = await fetch(`/api/admin/extensions/${id}/download`, {
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      credentials: "same-origin",
    });
    if (!response.ok) throw new Error("Download failed");
    const blob = await response.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = blobUrl;
    a.download = fallbackFileName;
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(blobUrl);
    document.body.removeChild(a);
  },

  delete: (id: number) =>
    api.delete(`/api/admin/extensions/${id}`),
};
