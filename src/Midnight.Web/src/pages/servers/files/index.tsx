import { ApiError } from "@/api/client";
import { serverFilesApi, type ArchiveFormat, type FileEntry } from "@/api/server-files";
import { EmptyState } from "@/components/shared/empty-state";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubTrigger,
  DropdownMenuSubContent,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown";
import { InputGroup, InputGroupAddon, InputGroupInput } from "@/components/ui/input-group";
import { Skeleton } from "@/components/ui/skeleton";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { useLocale, useT } from "@/i18n";
import {
  ArchiveIcon,
  ArrowLeftRightIcon,
  ChevronDownIcon,
  ChevronRightIcon,
  ChevronUpDownIcon,
  ChevronUpIcon,
  CopyIcon,
  DownloadIcon,
  FolderIcon,
  FolderOpenIcon,
  GlobeIcon,
  HorizontalDotsIcon,
  LockIcon,
  OctagonAlertIcon,
  PencilIcon,
  PlusIcon,
  RegenerateIcon,
  SearchIcon,
  TrashBinIcon,
  UploadIcon,
  XIcon,
} from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { formatBytes } from "@/lib/format";
import { notifyError, notifySuccess } from "@/lib/notify";
import { cn } from "@/utils";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState, type DragEvent, type MouseEvent, type ReactNode } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import { toast } from "sonner";
import { useServerContext } from "../hooks";
import { ChmodDialog, PullDialog, TextValueDialog } from "./dialogs";
import { FileTypeIcon } from "./file-icon";
import { UploadPanel } from "./uploads";
import { useUploads } from "./use-uploads";
import {
  editorUrl,
  filesQueryKey,
  filesUrl,
  formatRelative,
  isExtractable,
  joinPath,
  newFileUrl,
  normalizePath,
  parentPath,
  pathSegments,
  relativeToRoot,
  sortEntries,
  useFilePermissions,
  type FilePermissionSet,
  type SortKey,
} from "./utils";

export default function ServerFilesPage() {
  const { server } = useServerContext();
  const { t } = useT();
  const permissions = useFilePermissions();
  const [params] = useSearchParams();
  const directory = normalizePath(params.get("dir"));
  // Lives above the browser so uploads keep running while navigating folders
  const uploads = useUploads(server.uuid);

  if (!permissions.read)
    return <EmptyState icon={<LockIcon />} title={t("Files.NoAccess")} description={t("Files.NoAccessHint")} />;

  return (
    <>
      <FileBrowser key={directory} uuid={server.uuid} directory={directory} permissions={permissions} onUpload={uploads.add} />
      <UploadPanel items={uploads.items} onCancel={uploads.cancel} onCancelAll={uploads.cancelAll} onClear={uploads.clearFinished} />
    </>
  );
}

type DialogState =
  | { type: "new-folder" }
  | { type: "pull" }
  | { type: "rename"; entry: FileEntry }
  | { type: "move"; entries: FileEntry[] }
  | { type: "chmod"; entry: FileEntry };

const headerCell = "px-3 py-3 text-start text-xs font-medium text-gray-500 dark:text-gray-400";

function FileBrowser({ uuid, directory, permissions, onUpload }: {
  uuid: string;
  directory: string;
  permissions: FilePermissionSet;
  onUpload: (files: File[], directory: string) => void;
}) {
  const { t, formatDate } = useT();
  const locale = useLocale();
  const navigate = useNavigate();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const uploadInput = useRef<HTMLInputElement>(null);
  const dragDepth = useRef(0);

  const [sort, setSort] = useState<{ key: SortKey; descending: boolean }>({ key: "name", descending: false });
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
  const [anchor, setAnchor] = useState<number | null>(null);
  const [dialog, setDialog] = useState<DialogState | null>(null);
  const [dragging, setDragging] = useState(false);

  const query = useQuery({
    queryKey: filesQueryKey(uuid, directory),
    queryFn: () => serverFilesApi.list(uuid, directory),
    retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
  });

  const notFound = query.error instanceof ApiError && query.error.status === 404;

  // A folder that doesn't exist (anymore): back to the root
  useEffect(() => {
    if (!notFound || directory === "/") return;
    toast.error(t("Files.DirectoryNotFound", directory));
    navigate(filesUrl(uuid, "/"), { replace: true });
  }, [notFound, directory, navigate, uuid, t]);

  const entries = query.data ?? [];
  const term = search.trim().toLowerCase();
  const visible = sortEntries(term ? entries.filter((entry) => entry.name.toLowerCase().includes(term)) : entries, sort.key, sort.descending);
  const selection = entries.filter((entry) => selected.has(entry.name));
  const allSelected = visible.length > 0 && visible.every((entry) => selected.has(entry.name));

  const refreshCurrent = () => queryClient.invalidateQueries({ queryKey: filesQueryKey(uuid, directory) });
  const refreshAll = () => queryClient.invalidateQueries({ queryKey: filesQueryKey(uuid) });
  const clearSelection = () => {
    setSelected(new Set());
    setAnchor(null);
  };

  // ─── Selection ──────────────────────────────────────────────────────────

  const toggle = (entry: FileEntry, index: number, shift: boolean) => {
    const next = new Set(selected);

    if (shift && anchor !== null) {
      const [from, to] = anchor < index ? [anchor, index] : [index, anchor];
      const value = !selected.has(entry.name);
      for (const item of visible.slice(from, to + 1)) {
        if (value) next.add(item.name);
        else next.delete(item.name);
      }
    } else if (next.has(entry.name)) next.delete(entry.name);
    else next.add(entry.name);

    setSelected(next);
    setAnchor(index);
  };

  const toggleAll = () => {
    if (allSelected) clearSelection();
    else setSelected(new Set(visible.map((entry) => entry.name)));
  };

  // ─── Actions ────────────────────────────────────────────────────────────

  const run = async (action: () => Promise<unknown>, success: string, refresh = refreshCurrent) => {
    try {
      await action();
      notifySuccess(success);
    } catch (error) {
      notifyError(error);
    } finally {
      await refresh();
    }
  };

  const open = (entry: FileEntry) => {
    if (entry.isDirectory) navigate(filesUrl(uuid, joinPath(directory, entry.name)));
    else if (entry.isEditable && permissions.readContent) navigate(editorUrl(uuid, joinPath(directory, entry.name)));
  };

  const canOpen = (entry: FileEntry) => entry.isDirectory || (entry.isEditable && permissions.readContent);

  const download = async (entry: FileEntry) => {
    try {
      await serverFilesApi.download(uuid, joinPath(directory, entry.name));
    } catch (error) {
      notifyError(error);
    }
  };

  const copy = (entry: FileEntry) => run(() => serverFilesApi.copy(uuid, joinPath(directory, entry.name)), t("Files.Copied", entry.name));

  const compress = async (items: FileEntry[], format: ArchiveFormat) => {
    const id = toast.loading(t("Files.Compressing", items.length));

    try {
      const archive = await serverFilesApi.compress(uuid, directory, items.map((entry) => entry.name), format);
      toast.success(t("Files.Compressed", archive.name), { id });
      clearSelection();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : String(error), { id });
    } finally {
      await refreshCurrent();
    }
  };

  const extract = async (entry: FileEntry) => {
    const id = toast.loading(t("Files.Extracting", entry.name));

    try {
      await serverFilesApi.decompress(uuid, directory, entry.name);
      toast.success(t("Files.Extracted", entry.name), { id });
    } catch (error) {
      toast.error(error instanceof Error ? error.message : String(error), { id });
    } finally {
      await refreshCurrent();
    }
  };

  const remove = async (items: FileEntry[]) => {
    if (items.length === 0) return;

    const ok = await confirm({
      title: items.length === 1 ? t("Files.DeleteOneTitle") : t("Files.DeleteManyTitle", items.length),
      message: items.length === 1 ? t("Files.DeleteOneConfirm", items[0].name) : t("Files.DeleteManyConfirm", items.length),
      confirmLabel: t("Common.Delete"),
      cancelLabel: t("Common.Cancel"),
    });
    if (!ok) return;

    await run(() => serverFilesApi.delete(uuid, directory, items.map((entry) => entry.name)), t("Files.Deleted", items.length));
    clearSelection();
  };

  const moveTo = async (items: FileEntry[], destination: string) => {
    const target = normalizePath(destination);
    if (target === directory) return;

    await serverFilesApi.rename(
      uuid,
      "/",
      items.map((entry) => ({ from: relativeToRoot(joinPath(directory, entry.name)), to: relativeToRoot(joinPath(target, entry.name)) })),
    );
    notifySuccess(t("Files.Moved", items.length, target));
    clearSelection();
    await refreshAll();
  };

  const rename = async (entry: FileEntry, name: string) => {
    if (name === entry.name) return;
    await serverFilesApi.rename(uuid, directory, [{ from: entry.name, to: name }]);
    notifySuccess(t("Files.Renamed", name));
    // A name with folders moves the file
    await (name.includes("/") ? refreshAll() : refreshCurrent());
  };

  const chmod = async (entry: FileEntry, mode: string) => {
    await serverFilesApi.chmod(uuid, directory, [{ file: entry.name, mode }]);
    notifySuccess(t("Files.PermissionsChanged", entry.name, mode));
    await refreshCurrent();
  };

  const createFolder = async (name: string) => {
    await serverFilesApi.createFolder(uuid, directory, name);
    notifySuccess(t("Files.FolderCreated", name));
    await refreshAll();
  };

  const pull = async (url: string, fileName: string | null) => {
    await serverFilesApi.pull(uuid, url, directory, fileName);
    notifySuccess(t("Files.Pull.Started"));
    // The node downloads in the background
    window.setTimeout(() => void refreshCurrent(), 4000);
  };

  // ─── Upload / drag & drop ───────────────────────────────────────────────

  const hasFiles = (event: DragEvent) => permissions.create && Array.from(event.dataTransfer.types).includes("Files");

  const onDragEnter = (event: DragEvent) => {
    if (!hasFiles(event)) return;
    event.preventDefault();
    dragDepth.current++;
    setDragging(true);
  };

  const onDragLeave = (event: DragEvent) => {
    if (!hasFiles(event)) return;
    dragDepth.current = Math.max(0, dragDepth.current - 1);
    if (dragDepth.current === 0) setDragging(false);
  };

  const onDrop = (event: DragEvent) => {
    if (!hasFiles(event)) return;
    event.preventDefault();
    dragDepth.current = 0;
    setDragging(false);
    onUpload(Array.from(event.dataTransfer.files), directory);
  };

  // ─── Render ─────────────────────────────────────────────────────────────

  const sortHeader = (key: SortKey, label: string, className?: string) => {
    const active = sort.key === key;
    const Icon = !active ? ChevronUpDownIcon : sort.descending ? ChevronDownIcon : ChevronUpIcon;

    return (
      <th className={cn(headerCell, className)} aria-sort={active ? (sort.descending ? "descending" : "ascending") : undefined}>
        <button
          type="button"
          onClick={() => setSort({ key, descending: active ? !sort.descending : key !== "name" })}
          className={cn("inline-flex items-center gap-1 hover:text-gray-700 dark:hover:text-gray-200", active && "text-gray-700 dark:text-gray-200")}
        >
          {label}
          <Icon className="size-4" />
        </button>
      </th>
    );
  };

  const multiDownload = selection.length === 1 && !selection[0].isDirectory && permissions.readContent;

  return (
    <div className="space-y-4">
      {/* Breadcrumb + toolbar */}
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <Breadcrumbs uuid={uuid} directory={directory} />

        <div className="flex flex-wrap items-center gap-2">
          <InputGroup className="h-9 w-full sm:w-56">
            <InputGroupAddon align="inline-start">
              <SearchIcon className="size-4 text-gray-500 dark:text-gray-400" />
            </InputGroupAddon>
            <InputGroupInput placeholder={t("Files.Search")} value={search} onChange={(event) => setSearch(event.target.value)} />
          </InputGroup>

          <Tooltip>
            <TooltipTrigger
              render={
                <Button size="icon-sm" variant="outline" onClick={() => void query.refetch()} aria-label={t("Files.Refresh")} disabled={query.isFetching} />
              }
            >
              <RegenerateIcon className={cn("size-4", query.isFetching && "animate-spin")} />
            </TooltipTrigger>
            <TooltipContent>{t("Files.Refresh")}</TooltipContent>
          </Tooltip>

          {permissions.create && (
            <>
              <Button size="sm" variant="outline" onClick={() => setDialog({ type: "new-folder" })}>
                <FolderIcon className="size-4" />
                {t("Files.NewFolder")}
              </Button>
              <Button size="sm" variant="outline" onClick={() => navigate(newFileUrl(uuid, directory))}>
                <PlusIcon className="size-4" />
                {t("Files.NewFile")}
              </Button>
              <div className="flex">
                <Button size="sm" className="rounded-e-none" onClick={() => uploadInput.current?.click()}>
                  <UploadIcon className="size-4" />
                  {t("Files.Upload.Button")}
                </Button>
                <DropdownMenu>
                  <DropdownMenuTrigger
                    render={<Button size="icon-sm" className="rounded-s-none border-s border-s-white/20" aria-label={t("Files.MoreActions")} />}
                  >
                    <ChevronDownIcon className="size-4" />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end" className="min-w-56">
                    <DropdownMenuItem onClick={() => uploadInput.current?.click()}>
                      <UploadIcon />
                      {t("Files.Upload.Files")}
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => setDialog({ type: "pull" })}>
                      <GlobeIcon />
                      {t("Files.Pull.Title")}
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
              <input
                ref={uploadInput}
                type="file"
                multiple
                hidden
                onChange={(event) => {
                  onUpload(Array.from(event.target.files ?? []), directory);
                  event.target.value = "";
                }}
              />
            </>
          )}
        </div>
      </div>

      {/* List */}
      <div
        className="relative overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02]"
        onDragEnter={onDragEnter}
        onDragOver={(event) => hasFiles(event) && event.preventDefault()}
        onDragLeave={onDragLeave}
        onDrop={onDrop}
      >
        {selection.length > 0 && (
          <div className="flex flex-wrap items-center gap-2 border-b border-gray-100 bg-brand-500/[0.04] px-4 py-2.5 dark:border-gray-800 dark:bg-brand-500/[0.06]">
            <span className="me-auto text-sm font-medium text-gray-800 dark:text-white/90">{t("Files.SelectedCount", selection.length)}</span>
            {multiDownload && (
              <Button size="xs" variant="outline" onClick={() => void download(selection[0])}>
                <DownloadIcon className="size-4" />
                {t("Files.Actions.Download")}
              </Button>
            )}
            {permissions.update && (
              <Button size="xs" variant="outline" onClick={() => setDialog({ type: "move", entries: selection })}>
                <ArrowLeftRightIcon className="size-4" />
                {t("Files.Actions.Move")}
              </Button>
            )}
            {permissions.archive && (
              <DropdownMenu>
                <DropdownMenuTrigger render={<Button size="xs" variant="outline" />}>
                  <ArchiveIcon className="size-4" />
                  {t("Files.Actions.Compress")}
                  <ChevronDownIcon className="size-3.5" />
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="min-w-40">
                  <DropdownMenuItem onClick={() => void compress(selection, "tar.gz")}>.tar.gz</DropdownMenuItem>
                  <DropdownMenuItem onClick={() => void compress(selection, "zip")}>.zip</DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            )}
            {permissions.delete && (
              <Button size="xs" variant="destructive" onClick={() => void remove(selection)}>
                <TrashBinIcon className="size-4" />
                {t("Common.Delete")}
              </Button>
            )}
            <Button size="icon-xs" variant="ghost" onClick={clearSelection} aria-label={t("Files.ClearSelection")}>
              <XIcon className="size-4" />
            </Button>
          </div>
        )}

        <div className="max-w-full overflow-x-auto">
          <table className="w-full min-w-[640px] table-fixed">
            <thead className="border-b border-gray-100 dark:border-white/5">
              <tr>
                <th className="w-12 py-3 ps-4 pe-2">
                  <Checkbox
                    checked={allSelected}
                    disabled={visible.length === 0}
                    onCheckedChange={toggleAll}
                    aria-label={t("Files.SelectAll")}
                  />
                </th>
                {sortHeader("name", t("Common.Name"))}
                {sortHeader("size", t("Files.Size"), "w-28 text-end [&>button]:flex-row-reverse")}
                {sortHeader("modified", t("Files.Modified"), "w-40")}
                <th className={cn(headerCell, "hidden w-32 md:table-cell")}>{t("Files.Mode")}</th>
                <th className={cn(headerCell, "w-14")}>
                  <span className="sr-only">{t("Files.MoreActions")}</span>
                </th>
              </tr>
            </thead>
            <tbody className={cn("divide-y divide-gray-100 dark:divide-white/5", query.isFetching && query.data && "opacity-70 transition-opacity")}>
              {directory !== "/" && (
                <tr
                  className="cursor-pointer transition-colors hover:bg-gray-50 dark:hover:bg-white/[0.03]"
                  onClick={() => navigate(filesUrl(uuid, parentPath(directory)))}
                >
                  <td className="py-2.5 ps-4 pe-2" />
                  <td className="px-3 py-2.5">
                    <span className="flex items-center gap-3">
                      <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-gray-500/10 text-gray-500 dark:text-gray-400">
                        <FolderOpenIcon className="size-4" />
                      </span>
                      <span className="font-mono text-sm text-gray-600 dark:text-gray-300">..</span>
                    </span>
                  </td>
                  <td />
                  <td />
                  <td className="hidden md:table-cell" />
                  <td />
                </tr>
              )}

              {query.isPending && <SkeletonRows />}

              {visible.map((entry, index) => {
                const isSelected = selected.has(entry.name);
                const clickable = canOpen(entry);

                return (
                  <tr
                    key={entry.name}
                    data-selected={isSelected || undefined}
                    className={cn(
                      "group transition-colors hover:bg-gray-50 data-selected:bg-brand-500/[0.05] dark:hover:bg-white/[0.03] dark:data-selected:bg-brand-500/[0.08]",
                      clickable && "cursor-pointer",
                    )}
                    onMouseDown={(event) => event.shiftKey && event.preventDefault()}
                    onClick={(event: MouseEvent) => {
                      if (event.shiftKey || event.metaKey || event.ctrlKey) {
                        event.preventDefault();
                        toggle(entry, index, event.shiftKey);
                      } else open(entry);
                    }}
                  >
                    <td className="py-2.5 ps-4 pe-2" onClick={(event) => event.stopPropagation()}>
                      <Checkbox
                        checked={isSelected}
                        aria-label={t("Files.Select", entry.name)}
                        onClick={(event) => {
                          event.preventDefault();
                          toggle(entry, index, event.shiftKey);
                        }}
                      />
                    </td>
                    <td className="px-3 py-2.5">
                      <span className="flex min-w-0 items-center gap-3">
                        <FileTypeIcon entry={entry} />
                        {clickable ? (
                          <Link
                            to={entry.isDirectory ? filesUrl(uuid, joinPath(directory, entry.name)) : editorUrl(uuid, joinPath(directory, entry.name))}
                            onClick={(event) => event.stopPropagation()}
                            className="truncate text-sm font-medium text-gray-800 hover:text-brand-500 dark:text-white/90 dark:hover:text-brand-400"
                            title={entry.name}
                          >
                            {entry.name}
                          </Link>
                        ) : (
                          <span className="truncate text-sm font-medium text-gray-800 dark:text-white/90" title={entry.name}>{entry.name}</span>
                        )}
                      </span>
                    </td>
                    <td className="px-3 py-2.5 text-end text-sm text-gray-500 tabular-nums dark:text-gray-400">
                      {entry.isDirectory ? "–" : formatBytes(entry.size)}
                    </td>
                    <td className="px-3 py-2.5 text-sm text-gray-500 dark:text-gray-400">
                      <Tooltip>
                        <TooltipTrigger render={<span className="cursor-default" />}>{formatRelative(entry.modifiedAt, locale)}</TooltipTrigger>
                        <TooltipContent>{formatDate(entry.modifiedAt)}</TooltipContent>
                      </Tooltip>
                    </td>
                    <td className="hidden px-3 py-2.5 font-mono text-xs text-gray-500 md:table-cell dark:text-gray-400">
                      <span title={entry.modeBits}>{entry.mode}</span>
                    </td>
                    <td className="py-2.5 pe-3 text-end" onClick={(event) => event.stopPropagation()}>
                      <RowActions
                        entry={entry}
                        permissions={permissions}
                        onOpen={() => open(entry)}
                        canOpen={clickable}
                        onRename={() => setDialog({ type: "rename", entry })}
                        onMove={() => setDialog({ type: "move", entries: [entry] })}
                        onCopy={() => void copy(entry)}
                        onChmod={() => setDialog({ type: "chmod", entry })}
                        onDownload={() => void download(entry)}
                        onCompress={(format) => void compress([entry], format)}
                        onExtract={() => void extract(entry)}
                        onDelete={() => void remove([entry])}
                      />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        {query.isError && !notFound && (
          <div className="flex flex-col items-center gap-3 px-6 py-12 text-center">
            <span className="flex size-12 items-center justify-center rounded-full bg-red-50 text-red-500 dark:bg-red-500/10 dark:text-red-400">
              <OctagonAlertIcon className="size-6" />
            </span>
            <div>
              <p className="text-sm font-semibold text-gray-800 dark:text-white/90">{t("Files.LoadFailed")}</p>
              <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{query.error.message}</p>
            </div>
            <Button size="sm" variant="outline" onClick={() => void query.refetch()}>
              <RegenerateIcon className="size-4" />
              {t("Files.Retry")}
            </Button>
          </div>
        )}

        {query.isSuccess && visible.length === 0 && (
          <div className="flex flex-col items-center px-6 py-14 text-center">
            <span className="mb-4 flex size-14 items-center justify-center rounded-full bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400">
              {term ? <SearchIcon className="size-6" /> : <FolderOpenIcon className="size-6" />}
            </span>
            <p className="text-base font-semibold text-gray-800 dark:text-white/90">{term ? t("Files.NoMatches") : t("Files.EmptyFolder")}</p>
            <p className="mt-1 max-w-sm text-sm text-gray-500 dark:text-gray-400">
              {term ? t("Files.NoMatchesHint", search.trim()) : permissions.create ? t("Files.EmptyFolderHint") : t("Files.EmptyFolderReadOnly")}
            </p>
          </div>
        )}

        {query.isSuccess && entries.length > 0 && (
          <div className="flex items-center justify-between gap-3 border-t border-gray-100 px-4 py-2.5 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
            <span>{t("Files.Summary", entries.filter((entry) => entry.isDirectory).length, entries.filter((entry) => !entry.isDirectory).length)}</span>
            {permissions.create && <span className="hidden sm:inline">{t("Files.DropHint")}</span>}
          </div>
        )}

        {dragging && (
          <div className="pointer-events-none absolute inset-0 z-10 flex flex-col items-center justify-center gap-3 rounded-2xl border-2 border-dashed border-brand-500 bg-white/85 backdrop-blur-xs dark:bg-gray-950/85">
            <span className="flex size-14 items-center justify-center rounded-full bg-brand-500/10 text-brand-500 dark:text-brand-400">
              <UploadIcon className="size-6" />
            </span>
            <p className="text-sm font-semibold text-gray-800 dark:text-white/90">{t("Files.Upload.DropTitle")}</p>
            <p className="font-mono text-xs text-gray-500 dark:text-gray-400">{directory}</p>
          </div>
        )}
      </div>

      {dialog?.type === "new-folder" && (
        <TextValueDialog
          kind="path"
          icon={<FolderIcon />}
          title={t("Files.NewFolder")}
          description={t("Files.NewFolderHint", directory)}
          label={t("Common.Name")}
          initial=""
          placeholder="plugins"
          submitLabel={t("Files.Create")}
          onSubmit={createFolder}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.type === "rename" && (
        <TextValueDialog
          kind="path"
          icon={<PencilIcon />}
          title={t("Files.Actions.Rename")}
          label={t("Common.Name")}
          hint={t("Files.RenameHint")}
          initial={dialog.entry.name}
          submitLabel={t("Files.Actions.Rename")}
          onSubmit={(name) => rename(dialog.entry, name)}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.type === "move" && (
        <TextValueDialog
          kind="directory"
          icon={<ArrowLeftRightIcon />}
          title={dialog.entries.length === 1 ? t("Files.MoveOne", dialog.entries[0].name) : t("Files.MoveMany", dialog.entries.length)}
          label={t("Files.Destination")}
          hint={t("Files.DestinationHint")}
          initial={directory}
          submitLabel={t("Files.Actions.Move")}
          onSubmit={(destination) => moveTo(dialog.entries, destination)}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.type === "chmod" && (
        <ChmodDialog
          icon={<LockIcon />}
          fileName={dialog.entry.name}
          initial={dialog.entry.modeBits}
          onSubmit={(mode) => chmod(dialog.entry, mode)}
          onClose={() => setDialog(null)}
        />
      )}
      {dialog?.type === "pull" && <PullDialog icon={<GlobeIcon />} directory={directory} onSubmit={pull} onClose={() => setDialog(null)} />}
    </div>
  );
}

function Breadcrumbs({ uuid, directory }: { uuid: string; directory: string }) {
  const { t } = useT();
  const segments = pathSegments(directory);

  return (
    <nav aria-label={t("Files.Path")} className="flex min-w-0 items-center gap-1 overflow-x-auto rounded-xl border border-gray-200 bg-white px-2 py-1.5 text-sm scrollbar-none dark:border-gray-800 dark:bg-white/[0.02]">
      <Link
        to={filesUrl(uuid, "/")}
        className={cn(
          "flex shrink-0 items-center gap-1.5 rounded-md px-2 py-1 font-mono transition-colors hover:bg-gray-100 dark:hover:bg-white/5",
          segments.length === 0 ? "font-semibold text-gray-900 dark:text-white" : "text-gray-500 dark:text-gray-400",
        )}
      >
        <FolderIcon className="size-4" />
        {t("Files.Root")}
      </Link>
      {segments.map((segment, index) => {
        const last = index === segments.length - 1;

        return (
          <span key={segment.path} className="flex shrink-0 items-center gap-1">
            <ChevronRightIcon className="size-3.5 text-gray-400 rtl:rotate-180" />
            {last ? (
              <span className="rounded-md px-2 py-1 font-mono font-semibold text-gray-900 dark:text-white" aria-current="page">{segment.name}</span>
            ) : (
              <Link to={filesUrl(uuid, segment.path)} className="rounded-md px-2 py-1 font-mono text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-white/5 dark:hover:text-white">
                {segment.name}
              </Link>
            )}
          </span>
        );
      })}
    </nav>
  );
}

function SkeletonRows() {
  return Array.from({ length: 6 }, (_, index) => (
    <tr key={index}>
      <td className="py-3 ps-4 pe-2">
        <Skeleton className="size-4 rounded bg-gray-100 dark:bg-white/5" />
      </td>
      <td className="px-3 py-3">
        <span className="flex items-center gap-3">
          <Skeleton className="size-8 rounded-lg bg-gray-100 dark:bg-white/5" />
          <Skeleton className="h-3.5 rounded bg-gray-100 dark:bg-white/5" style={{ width: `${30 + ((index * 17) % 40)}%` }} />
        </span>
      </td>
      <td className="px-3 py-3">
        <Skeleton className="ms-auto h-3.5 w-14 rounded bg-gray-100 dark:bg-white/5" />
      </td>
      <td className="px-3 py-3">
        <Skeleton className="h-3.5 w-20 rounded bg-gray-100 dark:bg-white/5" />
      </td>
      <td className="hidden px-3 py-3 md:table-cell">
        <Skeleton className="h-3.5 w-20 rounded bg-gray-100 dark:bg-white/5" />
      </td>
      <td />
    </tr>
  ));
}

function RowActions({ entry, permissions, canOpen, onOpen, onRename, onMove, onCopy, onChmod, onDownload, onCompress, onExtract, onDelete }: {
  entry: FileEntry;
  permissions: FilePermissionSet;
  canOpen: boolean;
  onOpen: () => void;
  onRename: () => void;
  onMove: () => void;
  onCopy: () => void;
  onChmod: () => void;
  onDownload: () => void;
  onCompress: (format: ArchiveFormat) => void;
  onExtract: () => void;
  onDelete: () => void;
}) {
  const { t } = useT();

  const groups: ReactNode[][] = [
    [
      canOpen && (
        <DropdownMenuItem key="open" onClick={onOpen}>
          {entry.isDirectory ? <FolderOpenIcon /> : <PencilIcon />}
          {entry.isDirectory ? t("Files.Actions.Open") : t("Files.Actions.Edit")}
        </DropdownMenuItem>
      ),
      !entry.isDirectory && permissions.readContent && (
        <DropdownMenuItem key="download" onClick={onDownload}>
          <DownloadIcon />
          {t("Files.Actions.Download")}
        </DropdownMenuItem>
      ),
    ],
    [
      permissions.update && (
        <DropdownMenuItem key="rename" onClick={onRename}>
          <PencilIcon />
          {t("Files.Actions.Rename")}
        </DropdownMenuItem>
      ),
      permissions.update && (
        <DropdownMenuItem key="move" onClick={onMove}>
          <ArrowLeftRightIcon />
          {t("Files.Actions.Move")}
        </DropdownMenuItem>
      ),
      !entry.isDirectory && permissions.create && (
        <DropdownMenuItem key="copy" onClick={onCopy}>
          <CopyIcon />
          {t("Files.Actions.Copy")}
        </DropdownMenuItem>
      ),
      permissions.update && (
        <DropdownMenuItem key="chmod" onClick={onChmod}>
          <LockIcon />
          {t("Files.Actions.Permissions")}
        </DropdownMenuItem>
      ),
    ],
    [
      permissions.archive && (
        <DropdownMenuSub key="compress">
          <DropdownMenuSubTrigger showIcon>
            <ArchiveIcon />
            {t("Files.Actions.Compress")}
          </DropdownMenuSubTrigger>
          <DropdownMenuSubContent className="min-w-36">
            <DropdownMenuItem onClick={() => onCompress("tar.gz")}>.tar.gz</DropdownMenuItem>
            <DropdownMenuItem onClick={() => onCompress("zip")}>.zip</DropdownMenuItem>
          </DropdownMenuSubContent>
        </DropdownMenuSub>
      ),
      permissions.archive && isExtractable(entry) && (
        <DropdownMenuItem key="extract" onClick={onExtract}>
          <FolderOpenIcon />
          {t("Files.Actions.Extract")}
        </DropdownMenuItem>
      ),
    ],
    [
      permissions.delete && (
        <DropdownMenuItem key="delete" variant="destructive" onClick={onDelete}>
          <TrashBinIcon />
          {t("Common.Delete")}
        </DropdownMenuItem>
      ),
    ],
  ]
    .map((group) => group.filter(Boolean))
    .filter((group) => group.length > 0);

  if (groups.length === 0) return null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            size="icon-xs"
            variant="ghost"
            className="text-gray-400 opacity-70 group-hover:opacity-100 data-popup-open:opacity-100 dark:text-gray-400"
            aria-label={t("Files.ActionsFor", entry.name)}
          />
        }
      >
        <HorizontalDotsIcon className="size-5" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-52">
        {groups.map((group, index) => [index > 0 && <DropdownMenuSeparator key={`separator-${index}`} />, ...group])}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
