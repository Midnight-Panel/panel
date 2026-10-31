import { ApiError, api, tokens } from "./client";

// Mirror of Midnight.Shared.Http.Servers (Files) – /api/servers/{uuid}/files/...
// Paths are always relative to the server root, "/" = the root.

export interface FileEntry {
  name: string;
  size: number;
  isDirectory: boolean;
  isSymlink: boolean;
  /** "-rw-r--r--" */
  mode: string;
  /** "644" */
  modeBits: string;
  mimeType: string;
  /** Text file small enough for the editor */
  isEditable: boolean;
  modifiedAt: string;
}

export interface FileRename {
  from: string;
  to: string;
}

export interface FileChmod {
  file: string;
  /** Octal like "644" */
  mode: string;
}

export type ArchiveFormat = "tar.gz" | "zip";

export interface DownloadTicket {
  url: string;
}

const base = (uuid: string) => `/api/servers/${uuid}/files`;
const q = (value: string) => encodeURIComponent(value);

/** fetch with the bearer token of the session (one retry after a forced refresh), for non json bodies */
async function sendRaw(url: string, init: RequestInit): Promise<Response> {
  const run = (token: string | null) =>
    fetch(url, {
      ...init,
      credentials: "same-origin",
      headers: { ...(init.headers as Record<string, string> | undefined), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    });

  let token = await tokens.getAccessToken();
  let response = await run(token);

  if (response.status === 401 && token && !tokens.isImpersonating) {
    token = await tokens.getAccessToken(true);
    if (token) response = await run(token);
  }

  if (response.ok) return response;

  throw await errorFromResponse(response.status, response.statusText, await response.text().catch(() => ""));
}

function errorFromResponse(status: number, statusText: string, body: string): ApiError {
  try {
    const problem = JSON.parse(body);
    return new ApiError(status, problem.title ?? statusText, problem.detail ?? undefined, problem.errors ?? {});
  } catch {
    return new ApiError(status, statusText || `Request failed (${status})`, body && body.length < 300 ? body : undefined);
  }
}

export const serverFilesApi = {
  list: (uuid: string, directory: string) => api.get<FileEntry[]>(`${base(uuid)}/list?directory=${q(directory)}`),

  /** text/plain body; 413 when too large, 400 for binary files */
  contents: async (uuid: string, file: string) => (await sendRaw(`${base(uuid)}/contents?file=${q(file)}`, { method: "GET" })).text(),

  write: async (uuid: string, file: string, content: string) => {
    await sendRaw(`${base(uuid)}/write?file=${q(file)}`, {
      method: "POST",
      headers: { "Content-Type": "text/plain; charset=utf-8" },
      body: content,
    });
  },

  createFolder: (uuid: string, root: string, name: string) => api.post(`${base(uuid)}/create-folder`, { root, name }),

  rename: (uuid: string, root: string, files: FileRename[]) => api.put(`${base(uuid)}/rename`, { root, files }),

  copy: (uuid: string, location: string) => api.post(`${base(uuid)}/copy`, { location }),

  delete: (uuid: string, root: string, files: string[]) => api.post(`${base(uuid)}/delete`, { root, files }),

  compress: (uuid: string, root: string, files: string[], format: ArchiveFormat) =>
    api.post<FileEntry>(`${base(uuid)}/compress`, { root, files, format }),

  decompress: (uuid: string, root: string, file: string) => api.post(`${base(uuid)}/decompress`, { root, file }),

  chmod: (uuid: string, root: string, files: FileChmod[]) => api.post(`${base(uuid)}/chmod`, { root, files }),

  pull: (uuid: string, url: string, directory: string, fileName?: string | null) =>
    api.post(`${base(uuid)}/pull`, { url, directory, fileName: fileName || null }),

  downloadTicket: (uuid: string, file: string) => api.post<DownloadTicket>(`${base(uuid)}/download-ticket?file=${q(file)}`),

  /** Starts the browser download of a file with a single use ticket */
  download: async (uuid: string, file: string) => {
    const { url } = await serverFilesApi.downloadTicket(uuid, file);
    const link = document.createElement("a");
    link.href = url;
    link.rel = "noopener";
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    link.remove();
  },

  /** Uploads one file with progress (XMLHttpRequest, fetch has no upload progress) */
  upload: (uuid: string, directory: string, file: File, onProgress: (loaded: number, total: number) => void, signal?: AbortSignal) => {
    const url = `${base(uuid)}/upload?directory=${q(directory)}`;

    const attempt = (token: string | null) =>
      new Promise<XMLHttpRequest>((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open("POST", url);
        xhr.withCredentials = true;
        if (token) xhr.setRequestHeader("Authorization", `Bearer ${token}`);

        xhr.upload.onprogress = (event) => onProgress(event.loaded, event.lengthComputable ? event.total : file.size);
        xhr.onload = () => resolve(xhr);
        xhr.onerror = () => reject(new ApiError(0, "Network error"));
        xhr.onabort = () => reject(new DOMException("Upload aborted", "AbortError"));

        if (signal) {
          if (signal.aborted) return xhr.abort();
          signal.addEventListener("abort", () => xhr.abort(), { once: true });
        }

        const form = new FormData();
        form.append("files", file, file.name);
        xhr.send(form);
      });

    return (async () => {
      let token = await tokens.getAccessToken();
      let xhr = await attempt(token);

      if (xhr.status === 401 && token && !tokens.isImpersonating) {
        token = await tokens.getAccessToken(true);
        if (token) xhr = await attempt(token);
      }

      if (xhr.status < 200 || xhr.status >= 300) throw errorFromResponse(xhr.status, xhr.statusText, xhr.responseText);
    })();
  },
};
