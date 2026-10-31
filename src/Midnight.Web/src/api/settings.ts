import { api, tokens } from "./client";
import type { AdminSettings, AdminSettingsUpdateRequest } from "./types";

export const settingsApi = {
  getSettings: () => api.get<AdminSettings>("/api/admin/settings"),

  updateSettings: (data: AdminSettingsUpdateRequest) =>
    api.put<AdminSettings>("/api/admin/settings", data),

  downloadTroubleshooting: async () => {
    const token = await tokens.getAccessToken();
    const response = await fetch("/api/admin/settings/troubleshooting", {
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      credentials: "same-origin",
    });

    if (!response.ok) {
      throw new Error(`Failed to download troubleshooting bundle (${response.status})`);
    }

    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "troubleshooting.zip";
    document.body.appendChild(a);
    a.click();
    window.URL.revokeObjectURL(url);
    document.body.removeChild(a);
  },
};
