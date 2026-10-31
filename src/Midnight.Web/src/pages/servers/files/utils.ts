import type { FileEntry } from "@/api/server-files";
import { ServerPermissions } from "@/lib/server-permissions";
import { useServerContext } from "../hooks";

// ─── Paths ──────────────────────────────────────────────────────────────────

/** "/a//b/../c/" → "/a/c"; never leaves the root */
export function normalizePath(path: string | null | undefined): string {
  const parts: string[] = [];

  for (const part of (path ?? "/").replace(/\\/g, "/").split("/")) {
    if (!part || part === ".") continue;
    if (part === "..") parts.pop();
    else parts.push(part);
  }

  return "/" + parts.join("/");
}

export function joinPath(directory: string, name: string) {
  return normalizePath(`${directory}/${name}`);
}

export function parentPath(path: string) {
  return normalizePath(`${path}/..`);
}

export function baseName(path: string) {
  const normalized = normalizePath(path);
  return normalized.slice(normalized.lastIndexOf("/") + 1);
}

/** Root relative path without the leading slash, used by the rename (move) endpoint with root "/" */
export function relativeToRoot(path: string) {
  return normalizePath(path).slice(1);
}

/** Breadcrumb segments: [{ name, path }] */
export function pathSegments(path: string) {
  const parts = normalizePath(path).split("/").filter(Boolean);
  return parts.map((name, index) => ({ name, path: "/" + parts.slice(0, index + 1).join("/") }));
}

/** Rejects names with slashes or reserved segments */
export function isValidName(name: string) {
  const trimmed = name.trim();
  return trimmed.length > 0 && trimmed.length <= 255 && !/[/\\\0]/.test(trimmed) && trimmed !== "." && trimmed !== "..";
}

/** Names may contain folders ("config/a.yml") */
export function isValidRelativePath(path: string) {
  const trimmed = path.trim();
  return trimmed.length > 0 && trimmed.length <= 4096 && !trimmed.includes("\0") && trimmed.split("/").every((part) => part !== "..");
}

// ─── Types ──────────────────────────────────────────────────────────────────

const ARCHIVE = /\.(zip|tar|tar\.gz|tgz|gz|tar\.bz2|tbz2|tar\.xz|txz|7z|rar)$/i;
const EXTRACTABLE = /\.(zip|tar|tar\.gz|tgz|gz)$/i;
const IMAGE = /\.(png|jpe?g|gif|webp|bmp|ico|svg|tga)$/i;
const CODE = /\.(js|mjs|cjs|ts|tsx|jsx|json|ya?ml|xml|toml|ini|cfg|conf|properties|sh|bash|py|lua|java|kt|cs|go|rs|php|sql|html?|css|scss|md|env)$/i;
const BINARY = /\.(jar|exe|dll|so|bin|dat|db|sqlite|mca|nbt|class)$/i;

export type FileKind = "folder" | "archive" | "image" | "code" | "binary" | "text";

export function fileKind(entry: Pick<FileEntry, "name" | "isDirectory" | "mimeType">): FileKind {
  if (entry.isDirectory) return "folder";
  if (ARCHIVE.test(entry.name)) return "archive";
  if (IMAGE.test(entry.name) || entry.mimeType.startsWith("image/")) return "image";
  if (CODE.test(entry.name)) return "code";
  if (BINARY.test(entry.name)) return "binary";
  return "text";
}

export function isExtractable(entry: FileEntry) {
  return !entry.isDirectory && EXTRACTABLE.test(entry.name);
}

// ─── Sorting ────────────────────────────────────────────────────────────────

export type SortKey = "name" | "size" | "modified";

export function sortEntries(entries: FileEntry[], key: SortKey, descending: boolean) {
  const collator = new Intl.Collator(undefined, { numeric: true, sensitivity: "base" });
  const direction = descending ? -1 : 1;

  return [...entries].sort((a, b) => {
    // Directories always first
    if (a.isDirectory !== b.isDirectory) return a.isDirectory ? -1 : 1;

    if (key === "name") return collator.compare(a.name, b.name) * direction;

    const result = key === "size" ? a.size - b.size : Date.parse(a.modifiedAt) - Date.parse(b.modifiedAt);
    return result ? result * direction : collator.compare(a.name, b.name);
  });
}

// ─── Modes ──────────────────────────────────────────────────────────────────

/** "644" → [[r,w,x] owner, group, others] */
export function modeToBits(mode: string): boolean[][] {
  const digits = (mode.length > 3 ? mode.slice(-3) : mode.padStart(3, "0")).split("").map((digit) => Number.parseInt(digit, 8) || 0);
  return digits.map((digit) => [Boolean(digit & 4), Boolean(digit & 2), Boolean(digit & 1)]);
}

export function bitsToMode(bits: boolean[][]) {
  return bits.map(([r, w, x]) => String((r ? 4 : 0) + (w ? 2 : 0) + (x ? 1 : 0))).join("");
}

// ─── Permissions ────────────────────────────────────────────────────────────

/** Effective file permissions of the current user on the server */
export function useFilePermissions() {
  const { server, can } = useServerContext();
  const writable = !server.suspended;

  return {
    read: can(ServerPermissions.FileRead),
    readContent: can(ServerPermissions.FileReadContent),
    create: writable && can(ServerPermissions.FileCreate),
    update: writable && can(ServerPermissions.FileUpdate),
    delete: writable && can(ServerPermissions.FileDelete),
    archive: writable && can(ServerPermissions.FileArchive),
  };
}

export type FilePermissionSet = ReturnType<typeof useFilePermissions>;

// ─── Formatting ─────────────────────────────────────────────────────────────

export function formatRelative(value: string, locale: string) {
  const diff = (Date.parse(value) - Date.now()) / 1000;
  const format = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });
  const steps: Array<[Intl.RelativeTimeFormatUnit, number]> = [
    ["year", 31_536_000],
    ["month", 2_592_000],
    ["week", 604_800],
    ["day", 86_400],
    ["hour", 3600],
    ["minute", 60],
  ];

  for (const [unit, seconds] of steps)
    if (Math.abs(diff) >= seconds) return format.format(Math.round(diff / seconds), unit);

  return format.format(Math.round(diff), "second");
}

export function filesQueryKey(uuid: string, directory?: string) {
  return directory === undefined ? (["servers", uuid, "files"] as const) : (["servers", uuid, "files", normalizePath(directory)] as const);
}

export function filesUrl(uuid: string, directory: string) {
  const dir = normalizePath(directory);
  return dir === "/" ? `/servers/${uuid}/files` : `/servers/${uuid}/files?dir=${encodeURIComponent(dir)}`;
}

export function editorUrl(uuid: string, file: string) {
  return `/servers/${uuid}/files/edit?file=${encodeURIComponent(normalizePath(file))}`;
}

export function newFileUrl(uuid: string, directory: string) {
  return `/servers/${uuid}/files/edit?dir=${encodeURIComponent(normalizePath(directory))}`;
}
