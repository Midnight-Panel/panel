import { ApiError } from "@/api/client";
import type { ServerLimits, ServerState, ServerStatus } from "@/api/types";
import { formatBytes } from "./format";

type Translate = (key: string, ...args: Array<string | number>) => string;

const MIB = 1024 * 1024;

/** Defaults of ServerLimitsDto / ServerFeatureLimitsDto */
export const defaultLimits: ServerLimits = {
  cpu: 100,
  memory: 1024,
  swap: 0,
  disk: 5120,
  ioWeight: 500,
  threads: "",
  oomKillerDisabled: false,
};

export const defaultFeatureLimits = { databases: 0, allocations: 0, backups: 0 };

/** Same pattern as ServerLimitsDto.Threads */
export const threadsPattern = /^$|^[0-9]+(-[0-9]+)?(,[0-9]+(-[0-9]+)?)*$/;

export function formatMib(mib: number) {
  return formatBytes(mib * MIB);
}

/** "1.5 cores" style text of a cpu limit in percent */
export function formatCpu(cpu: number, t: Translate) {
  if (cpu <= 0) return t("Servers.Unlimited");
  return t("Servers.CpuCores", Number((cpu / 100).toFixed(2)));
}

/** Short summary like "100 % CPU · 1.0 GB RAM · 5.0 GB disk" */
export function limitsSummary(limits: ServerLimits, t: Translate) {
  const cpu = limits.cpu > 0 ? `${limits.cpu} %` : "∞";
  const disk = limits.disk > 0 ? formatMib(limits.disk) : "∞";
  return t("Servers.LimitsSummary", cpu, formatMib(limits.memory), disk);
}

/** Helper text of a MiB input: "= 1.0 GB" */
export function mibHint(mib: number, t: Translate, zeroLabel?: string, minusOneLabel?: string) {
  if (!Number.isFinite(mib)) return "";
  if (mib === 0 && zeroLabel) return zeroLabel;
  if (mib === -1 && minusOneLabel) return minusOneLabel;
  if (mib < 0) return "";
  return t("Servers.Equals", formatMib(mib));
}

export function formatUptime(seconds: number) {
  if (seconds <= 0) return "-";

  const days = Math.floor(seconds / 86_400);
  const hours = Math.floor((seconds % 86_400) / 3_600);
  const minutes = Math.floor((seconds % 3_600) / 60);

  if (days > 0) return `${days}d ${hours}h ${minutes}m`;
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m ${Math.floor(seconds % 60)}s`;
}

export type StateTone = "success" | "warning" | "error" | "gray" | "info";

export function stateTone(state: ServerState | undefined): StateTone {
  switch (state) {
    case "Online":
      return "success";
    case "Starting":
    case "Stopping":
      return "warning";
    case "Installing":
      return "info";
    case "Offline":
      return "error";
    default:
      return "gray";
  }
}

export function statusTone(status: ServerStatus): StateTone {
  switch (status) {
    case "Ready":
      return "success";
    case "Installing":
      return "info";
    case "InstallFailed":
      return "error";
  }
}

/** Which power buttons make sense in a state */
export function powerAvailability(state: ServerState | undefined, blocked: boolean) {
  const known = state !== undefined && !blocked;

  return {
    start: known && state === "Offline",
    stop: known && (state === "Online" || state === "Starting"),
    restart: known && (state === "Online" || state === "Starting"),
    kill: known && state !== "Offline" && state !== "Installing",
  };
}

/** Copies text to the clipboard, false when the browser refused */
export async function copyText(text: string) {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}

/**
 * Client side port of ShardVariableValidator (required, string, boolean, numeric, integer, min, max, between, regex).
 * The api validates the same rules, this only gives earlier feedback.
 */
export function validateVariable(rawValue: string | null | undefined, rules: string, t: Translate): string | undefined {
  const value = rawValue?.trim() ?? "";
  const list = rules.split("|").map((rule) => rule.trim()).filter(Boolean);
  const lower = (rule: string) => rule.toLowerCase();

  if (!value) return list.some((rule) => lower(rule) === "required") ? t("Validation.Required") : undefined;

  const asNumber = /^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$/.test(value) ? Number(value) : Number.NaN;
  const isNumber = !Number.isNaN(asNumber);

  for (const rule of list) {
    const name = lower(rule);

    if (name === "boolean" && !["true", "false", "0", "1"].includes(value.toLowerCase())) return t("Servers.Rules.Boolean");
    if (name === "numeric" && !isNumber) return t("Servers.Rules.Numeric");
    if (name === "integer" && !/^[-+]?\d+$/.test(value)) return t("Servers.Rules.Integer");

    if (name.startsWith("min:")) {
      const min = Number(rule.slice(4));
      if (Number.isNaN(min)) continue;
      if (isNumber ? asNumber < min : value.length < min) return t(isNumber ? "Servers.Rules.Min" : "Servers.Rules.MinLength", min);
    }

    if (name.startsWith("max:")) {
      const max = Number(rule.slice(4));
      if (Number.isNaN(max)) continue;
      if (isNumber ? asNumber > max : value.length > max) return t(isNumber ? "Servers.Rules.Max" : "Servers.Rules.MaxLength", max);
    }

    if (name.startsWith("between:")) {
      const [min, max] = rule.slice(8).split(",").map(Number);
      if (min === undefined || max === undefined || Number.isNaN(min) || Number.isNaN(max)) continue;
      const actual = isNumber ? asNumber : value.length;
      if (actual < min || actual > max) return t(isNumber ? "Servers.Rules.Between" : "Servers.Rules.BetweenLength", min, max);
    }

    if (name.startsWith("regex:")) {
      try {
        // .NET patterns are mostly compatible, invalid ones are skipped like on the server
        if (!new RegExp(rule.slice(6)).test(value)) return t("Servers.Rules.Regex");
      } catch {
        continue;
      }
    }
  }

  return undefined;
}

export function isBooleanRule(rules: string) {
  return rules.split("|").some((rule) => rule.trim().toLowerCase() === "boolean");
}

export function isRequiredRule(rules: string) {
  return rules.split("|").some((rule) => rule.trim().toLowerCase() === "required");
}

export interface AllocationOption {
  id: number;
  ipAddress: string;
  port: number;
  notes?: string | null;
}

export function allocationLabel(allocation: Pick<AllocationOption, "ipAddress" | "port">) {
  return `${allocation.ipAddress}:${allocation.port}`;
}

/**
 * Field => message of a problem response. The api writes "errors" either as { field: message }
 * (e.g. invalid variables, keyed by EnvVariable) or as { field: [messages] } (model validation).
 */
export function problemFieldErrors(error: unknown): Record<string, string> {
  if (!(error instanceof ApiError)) return {};

  const result: Record<string, string> = {};

  for (const [key, value] of Object.entries(error.errors as Record<string, unknown>)) {
    const message = Array.isArray(value)
      ? value.filter((entry): entry is string => typeof entry === "string").join(" ")
      : typeof value === "string" ? value : "";

    if (message) result[key] = message;
  }

  return result;
}
