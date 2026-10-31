import type { LoginResponse, PagedQuery } from "./types";

/** Error response of the api, parsed from ProblemDetails */
export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail?: string;
  readonly errors: Record<string, string[]>;

  constructor(status: number, title: string, detail?: string, errors: Record<string, string[]> = {}) {
    const validation = Object.values(errors).flat();
    super(validation.length > 0 ? validation.join(" ") : detail || title);

    this.status = status;
    this.title = title;
    this.detail = detail;
    this.errors = errors;
  }
}

const REFRESH_MARGIN_MS = 30_000;

/**
 * Keeps the access token in memory and refreshes it with the http only refresh cookie.
 * While impersonating, the impersonation token is used and never refreshed.
 */
class TokenManager {
  private accessToken: string | null = null;
  private expiresAt = 0;
  private impersonationToken: string | null = null;
  private impersonationExpiresAt = 0;
  private refreshing: Promise<boolean> | null = null;

  onImpersonationExpired: (() => void) | null = null;

  get isImpersonating() {
    return this.impersonationToken !== null;
  }

  setAccessToken(response: LoginResponse) {
    this.accessToken = response.accessToken ?? null;
    this.expiresAt = response.expiresAt ? Date.parse(response.expiresAt) : 0;
  }

  startImpersonation(token: string, expiresAt: string) {
    this.impersonationToken = token;
    this.impersonationExpiresAt = Date.parse(expiresAt);
  }

  stopImpersonation() {
    this.impersonationToken = null;
  }

  clear() {
    this.accessToken = null;
    this.impersonationToken = null;
  }

  async getAccessToken(forceRefresh = false): Promise<string | null> {
    if (this.impersonationToken) {
      if (this.impersonationExpiresAt > Date.now()) return this.impersonationToken;

      this.impersonationToken = null;
      this.onImpersonationExpired?.();
    }

    if (!forceRefresh && this.accessToken && this.expiresAt - REFRESH_MARGIN_MS > Date.now())
      return this.accessToken;

    // Concurrent callers share one refresh request
    this.refreshing ??= this.refresh().finally(() => (this.refreshing = null));

    return (await this.refreshing) ? this.accessToken : null;
  }

  private async refresh(): Promise<boolean> {
    const response = await fetch("/api/auth/refresh", { method: "POST", credentials: "same-origin" });

    if (!response.ok) {
      this.accessToken = null;
      return false;
    }

    const login = (await response.json()) as LoginResponse;

    if (!login.accessToken) return false;

    this.setAccessToken(login);
    return true;
  }
}

export const tokens = new TokenManager();

/** Called when the api rejects the session (logged out elsewhere, locked, ...) */
let unauthorizedHandler: (() => void) | null = null;

export function onUnauthorized(handler: (() => void) | null) {
  unauthorizedHandler = handler;
}

async function send(method: string, url: string, body?: unknown): Promise<Response> {
  const build = (token: string | null): RequestInit => {
    const headers: Record<string, string> = {};

    if (token) headers.Authorization = `Bearer ${token}`;

    if (body instanceof FormData) return { method, headers, body, credentials: "same-origin" };

    if (body !== undefined) headers["Content-Type"] = "application/json";

    return {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      credentials: "same-origin"
    };
  };

  let token = await tokens.getAccessToken();
  let response = await fetch(url, build(token));

  // The token may have been invalidated server side (password change, logout everywhere), retry once
  if (response.status === 401 && token && !tokens.isImpersonating) {
    token = await tokens.getAccessToken(true);

    if (token) response = await fetch(url, build(token));
  }

  if (response.ok) return response;

  if (response.status === 401) unauthorizedHandler?.();

  throw await toError(response);
}

async function toError(response: Response): Promise<ApiError> {
  try {
    const problem = await response.json();
    return new ApiError(response.status, problem.title ?? response.statusText, problem.detail ?? undefined, problem.errors ?? {});
  } catch {
    return new ApiError(response.status, response.statusText || `Request failed (${response.status})`);
  }
}

async function json<T>(response: Response): Promise<T> {
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  get: async <T>(url: string) => json<T>(await send("GET", url)),
  post: async <T = void>(url: string, body?: unknown) => json<T>(await send("POST", url, body)),
  put: async <T = void>(url: string, body?: unknown) => json<T>(await send("PUT", url, body)),
  delete: async (url: string) => {
    await send("DELETE", url);
  },
  upload: async (url: string, file: File) => {
    const form = new FormData();
    form.append("file", file, file.name);
    await send("PUT", url, form);
  }
};

export function toQueryString(query: PagedQuery): string {
  const params = new URLSearchParams({ page: String(query.page), pageSize: String(query.pageSize) });

  if (query.search) params.set("search", query.search);

  if (query.sortBy) {
    params.set("sortBy", query.sortBy);
    params.set("sortDescending", String(!!query.sortDescending));
  }

  return params.toString();
}
