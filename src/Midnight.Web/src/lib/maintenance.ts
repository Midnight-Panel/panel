/**
 * Detects when the panel goes away under an open page: during updates the proxy answers with its maintenance
 * page (503, html) and while the panel container restarts with 502/504 or not at all.
 */
type Listener = () => void;

const listeners = new Set<Listener>();

export function onPanelUnavailable(listener: Listener) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function reportPanelUnavailable() {
  listeners.forEach((listener) => listener());
}

/** Gateway errors of the proxy. The panel's own errors are problem+json and don't count (e.g. 502 for an offline node). */
export function isMaintenanceResponse(response: Response) {
  return [502, 503, 504].includes(response.status) && !(response.headers.get("content-type") ?? "").includes("json");
}

/** fetch that reports maintenance responses and network failures, otherwise behaves exactly like fetch */
export async function trackedFetch(input: RequestInfo | URL, init?: RequestInit) {
  try {
    const response = await fetch(input, init);
    if (isMaintenanceResponse(response)) reportPanelUnavailable();
    return response;
  } catch (error) {
    // TypeError = no response at all (connection refused, proxy restarting), aborts are not a maintenance sign
    if (error instanceof TypeError) reportPanelUnavailable();
    throw error;
  }
}

export async function isPanelHealthy() {
  try {
    const response = await fetch("/api/health", { cache: "no-store", headers: { Accept: "application/json" } });
    if (!response.ok) return false;

    const body = (await response.json()) as { status?: string };
    return body.status === "ok";
  } catch {
    return false;
  }
}
