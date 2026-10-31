import { ApiError } from "@/api/client";
import { serverFilesApi } from "@/api/server-files";
import { EmptyState } from "@/components/shared/empty-state";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Spinner } from "@/components/ui/spinner";
import { useTheme } from "@/contexts/theme-context";
import { useT } from "@/i18n";
import { ArrowLeftIcon, AspectFullscreenIcon, DownloadIcon, EyeIcon, FloppyDiscIcon, LockIcon, OctagonAlertIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { formatBytes } from "@/lib/format";
import { notifyError, notifySuccess } from "@/lib/notify";
import { cn } from "@/utils";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { lazy, Suspense, useEffect, useRef, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import { useServerContext } from "../hooks";
import { FileTypeIcon } from "./file-icon";
import { editorLanguages, languageForFile, languageLabels, type EditorLanguage } from "./languages";
import { baseName, editorUrl, filesQueryKey, filesUrl, isValidRelativePath, joinPath, normalizePath, parentPath, useFilePermissions } from "./utils";

// CodeMirror is only loaded when the editor is opened
const CodeEditor = lazy(() => import("./code-editor"));

function contentQueryKey(uuid: string, file: string) {
  return [...filesQueryKey(uuid), "content", file] as const;
}

export default function ServerFileEditorPage() {
  const { server } = useServerContext();
  const { t } = useT();
  const permissions = useFilePermissions();
  const [params] = useSearchParams();
  const fileParam = params.get("file");
  const file = fileParam ? normalizePath(fileParam) : null;
  const directory = file ? parentPath(file) : normalizePath(params.get("dir"));

  if (file === null) {
    if (!permissions.create)
      return <EmptyState icon={<LockIcon />} title={t("Files.NoAccess")} description={t("Files.Editor.NoCreate")} />;

    return <EditorPanel key={`new:${directory}`} uuid={server.uuid} directory={directory} file={null} initial="" readOnly={false} />;
  }

  if (!permissions.readContent)
    return <EmptyState icon={<LockIcon />} title={t("Files.NoAccess")} description={t("Files.Editor.NoRead")} />;

  return <ExistingFile key={file} uuid={server.uuid} file={file} readOnly={!permissions.update} />;
}

function ExistingFile({ uuid, file, readOnly }: { uuid: string; file: string; readOnly: boolean }) {
  const { t } = useT();
  const directory = parentPath(file);

  const query = useQuery({
    queryKey: contentQueryKey(uuid, file),
    queryFn: () => serverFilesApi.contents(uuid, file),
    staleTime: Infinity,
    gcTime: 0,
    retry: false,
  });

  if (query.isPending) return <EditorSkeleton />;

  if (query.isError) {
    const status = query.error instanceof ApiError ? query.error.status : 0;
    const message =
      status === 413 ? t("Files.Editor.TooLarge") : status === 400 ? t("Files.Editor.Binary") : status === 404 ? t("Files.Editor.NotFound") : query.error.message;

    return <EditorError uuid={uuid} file={file} directory={directory} message={message} canDownload={status === 413 || status === 400} />;
  }

  return <EditorPanel uuid={uuid} directory={directory} file={file} initial={query.data} readOnly={readOnly} />;
}

function EditorPanel({ uuid, directory, file, initial, readOnly }: {
  uuid: string;
  directory: string;
  /** null = new file */
  file: string | null;
  initial: string;
  readOnly: boolean;
}) {
  const { t } = useT();
  const navigate = useNavigate();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const { theme } = useTheme();

  const [content, setContent] = useState(initial);
  const [saved, setSaved] = useState(initial);
  const [name, setName] = useState("");
  const [nameError, setNameError] = useState<string | null>(null);
  const [language, setLanguage] = useState<EditorLanguage | null>(null);
  const [saving, setSaving] = useState(false);
  const [fullscreen, setFullscreen] = useState(false);

  const isNew = file === null;
  const fileName = isNew ? name.trim() || "untitled.txt" : baseName(file);
  const dirty = content !== saved || (isNew && content.length > 0);
  const resolvedLanguage = language ?? languageForFile(fileName);
  const size = new Blob([content]).size;

  const confirmLeave = () =>
    confirm({
      title: t("Files.Editor.UnsavedTitle"),
      message: t("Files.Editor.UnsavedMessage"),
      confirmLabel: t("Files.Editor.Discard"),
      cancelLabel: t("Files.Editor.KeepEditing"),
    });

  // Unsaved changes guard: reloads/tab close, and in-app links (BrowserRouter has no blocker api)
  const dirtyRef = useRef(dirty);
  useEffect(() => {
    dirtyRef.current = dirty;
  }, [dirty]);

  useEffect(() => {
    if (!dirty) return;

    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };

    const onClick = (event: MouseEvent) => {
      if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      const anchor = (event.target as Element | null)?.closest?.("a[href]") as HTMLAnchorElement | null;
      if (!anchor || anchor.target === "_blank" || anchor.hasAttribute("download")) return;

      const url = new URL(anchor.href);
      if (url.origin !== window.location.origin) return;

      event.preventDefault();
      event.stopPropagation();

      void confirm({
        title: t("Files.Editor.UnsavedTitle"),
        message: t("Files.Editor.UnsavedMessage"),
        confirmLabel: t("Files.Editor.Discard"),
        cancelLabel: t("Files.Editor.KeepEditing"),
      }).then((ok) => ok && navigate(url.pathname + url.search + url.hash));
    };

    window.addEventListener("beforeunload", onBeforeUnload);
    document.addEventListener("click", onClick, true);

    return () => {
      window.removeEventListener("beforeunload", onBeforeUnload);
      document.removeEventListener("click", onClick, true);
    };
  }, [dirty, confirm, navigate, t]);

  // Esc leaves the fullscreen mode
  useEffect(() => {
    if (!fullscreen) return;
    const onKey = (event: KeyboardEvent) => event.key === "Escape" && setFullscreen(false);
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [fullscreen]);

  const close = async () => {
    if (dirtyRef.current && !(await confirmLeave())) return;
    navigate(filesUrl(uuid, directory));
  };

  const save = async (andClose = false) => {
    if (readOnly || saving) return;

    let target = file;

    if (isNew) {
      const trimmed = name.trim();
      if (!trimmed || !isValidRelativePath(trimmed)) {
        setNameError(trimmed ? t("Files.Validation.Path") : t("Validation.Required"));
        return;
      }
      target = joinPath(directory, trimmed);
    }

    setSaving(true);

    try {
      await serverFilesApi.write(uuid, target!, content);
      setSaved(content);
      dirtyRef.current = false;
      notifySuccess(t("Files.Editor.Saved", baseName(target!)));
      void queryClient.invalidateQueries({ queryKey: filesQueryKey(uuid, parentPath(target!)) });

      if (andClose) navigate(filesUrl(uuid, directory));
      else if (isNew) {
        // Continue editing the created file
        queryClient.setQueryData(contentQueryKey(uuid, target!), content);
        navigate(editorUrl(uuid, target!), { replace: true });
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 413) notifyError(t("Files.Editor.SaveTooLarge"));
      else notifyError(error);
    } finally {
      setSaving(false);
    }
  };

  const saveRef = useRef(save);
  useEffect(() => {
    saveRef.current = save;
  });

  // Stable callback for the editor keymap (Ctrl/Cmd+S)
  const [onSaveShortcut] = useState(() => () => void saveRef.current());

  const download = async () => {
    if (!file) return;
    try {
      await serverFilesApi.download(uuid, file);
    } catch (error) {
      notifyError(error);
    }
  };

  return (
    <div
      className={cn(
        "flex flex-col overflow-hidden border border-gray-200 bg-white dark:border-gray-800 dark:bg-[#0e0e10]",
        fullscreen ? "fixed inset-0 z-[90] rounded-none" : "rounded-2xl",
      )}
    >
      {/* Header */}
      <div className="flex flex-col gap-3 border-b border-gray-200 px-4 py-3 lg:flex-row lg:items-center lg:justify-between dark:border-gray-800">
        <div className="flex min-w-0 items-center gap-3">
          <Button size="icon-sm" variant="ghost" onClick={() => void close()} aria-label={t("Files.Editor.Back")}>
            <ArrowLeftIcon className="size-4" />
          </Button>
          <FileTypeIcon entry={{ name: fileName, isDirectory: false, isSymlink: false, mimeType: "" }} />
          {isNew ? (
            <div className="flex min-w-0 flex-1 items-center gap-1">
              <Link to={filesUrl(uuid, directory)} className="hidden shrink-0 font-mono text-sm text-gray-500 hover:text-gray-800 sm:inline dark:text-gray-400 dark:hover:text-white">
                {directory === "/" ? "/" : `${directory}/`}
              </Link>
              <div className="min-w-0 flex-1">
                <Input
                  autoFocus
                  value={name}
                  spellCheck={false}
                  placeholder={t("Files.Editor.NamePlaceholder")}
                  aria-label={t("Files.Editor.FileName")}
                  aria-invalid={nameError ? "true" : undefined}
                  className="h-9 w-full max-w-xs font-mono"
                  onChange={(event) => {
                    setName(event.target.value);
                    setNameError(null);
                  }}
                  onKeyDown={(event) => event.key === "Enter" && void save()}
                />
                {nameError && <p className="mt-1 text-xs text-red-500">{nameError}</p>}
              </div>
            </div>
          ) : (
            <div className="min-w-0">
              <p className="flex items-center gap-2 truncate font-mono text-sm font-semibold text-gray-900 dark:text-white">
                {fileName}
                {dirty && <span className="size-2 shrink-0 rounded-full bg-amber-500" title={t("Files.Editor.Unsaved")} />}
              </p>
              <Link to={filesUrl(uuid, directory)} className="block truncate font-mono text-xs text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white">
                {directory}
              </Link>
            </div>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {readOnly && (
            <span className="inline-flex items-center gap-1.5 rounded-lg bg-gray-100 px-2.5 py-1.5 text-xs font-medium text-gray-600 dark:bg-white/5 dark:text-gray-300">
              <EyeIcon className="size-3.5" />
              {t("Files.Editor.ReadOnly")}
            </span>
          )}
          <Select<EditorLanguage>
            value={resolvedLanguage}
            items={editorLanguages.map((value) => ({ value, label: languageLabels[value] }))}
            onValueChange={(value) => value && setLanguage(value)}
          >
            <SelectTrigger size="sm" className="w-40" aria-label={t("Files.Editor.Language")}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {editorLanguages.map((value) => (
                <SelectItem key={value} value={value}>{languageLabels[value]}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          {!isNew && (
            <Button size="icon-sm" variant="outline" onClick={() => void download()} aria-label={t("Files.Actions.Download")} title={t("Files.Actions.Download")}>
              <DownloadIcon className="size-4" />
            </Button>
          )}
          <Button
            size="icon-sm"
            variant="outline"
            onClick={() => setFullscreen((value) => !value)}
            aria-label={t("Files.Editor.Fullscreen")}
            title={t("Files.Editor.Fullscreen")}
            aria-pressed={fullscreen}
          >
            <AspectFullscreenIcon className="size-4" />
          </Button>
          {!readOnly && (
            <>
              <Button size="sm" variant="outline" disabled={saving || (!dirty && !isNew)} onClick={() => void save(true)}>
                {t("Files.Editor.SaveAndClose")}
              </Button>
              <Button size="sm" disabled={saving || (!dirty && !isNew)} onClick={() => void save()}>
                {saving ? <Spinner className="size-4" /> : <FloppyDiscIcon className="size-4" />}
                {isNew ? t("Files.Create") : t("Common.Save")}
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Editor */}
      <div className={cn("min-h-0", fullscreen ? "flex-1" : "h-[calc(100dvh-22rem)] min-h-[28rem]")}>
        <Suspense fallback={<div className="flex h-full items-center justify-center text-brand-500"><Spinner className="size-6" /></div>}>
          <CodeEditor
            value={content}
            onChange={setContent}
            fileName={fileName}
            language={resolvedLanguage}
            dark={theme === "dark"}
            readOnly={readOnly}
            onSave={onSaveShortcut}
          />
        </Suspense>
      </div>

      {/* Status bar */}
      <div className="flex items-center justify-between gap-3 border-t border-gray-200 px-4 py-2 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
        <span className="flex items-center gap-3">
          <span>{languageLabels[resolvedLanguage]}</span>
          <span>{formatBytes(size)}</span>
          <span>{t("Files.Editor.Lines", content.split("\n").length)}</span>
        </span>
        <span className="flex items-center gap-1.5">
          {isNew ? (
            <>
              <span className="size-1.5 rounded-full bg-gray-400" />
              {t("Files.Editor.NotCreated")}
            </>
          ) : dirty ? (
            <>
              <span className="size-1.5 rounded-full bg-amber-500" />
              {t("Files.Editor.Unsaved")}
            </>
          ) : (
            <>
              <span className="size-1.5 rounded-full bg-green-500" />
              {t("Files.Editor.AllSaved")}
            </>
          )}
          {!readOnly && <kbd className="ms-2 hidden rounded border border-gray-200 px-1.5 font-sans text-[10px] sm:inline dark:border-gray-700">{t("Files.Editor.SaveShortcut")}</kbd>}
        </span>
      </div>
    </div>
  );
}

function EditorSkeleton() {
  return (
    <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-[#0e0e10]">
      <div className="flex items-center gap-3 border-b border-gray-200 px-4 py-3 dark:border-gray-800">
        <Skeleton className="size-8 rounded-lg bg-gray-100 dark:bg-white/5" />
        <Skeleton className="h-4 w-48 rounded bg-gray-100 dark:bg-white/5" />
        <Skeleton className="ms-auto h-9 w-24 rounded-lg bg-gray-100 dark:bg-white/5" />
      </div>
      <div className="h-[calc(100dvh-22rem)] min-h-[28rem] space-y-3 p-5">
        {Array.from({ length: 12 }, (_, index) => (
          <Skeleton key={index} className="h-3 rounded bg-gray-100 dark:bg-white/5" style={{ width: `${20 + ((index * 37) % 60)}%` }} />
        ))}
      </div>
    </div>
  );
}

function EditorError({ uuid, file, directory, message, canDownload }: { uuid: string; file: string; directory: string; message: string; canDownload: boolean }) {
  const { t } = useT();

  const download = async () => {
    try {
      await serverFilesApi.download(uuid, file);
    } catch (error) {
      notifyError(error);
    }
  };

  return (
    <EmptyState icon={<OctagonAlertIcon />} title={t("Files.Editor.CannotOpen", baseName(file))} description={message}>
      <div className="mt-6 flex flex-wrap justify-center gap-2">
        <Button variant="outline" size="sm" render={<Link to={filesUrl(uuid, directory)} />} nativeButton={false}>
          <ArrowLeftIcon className="size-4" />
          {t("Files.Editor.BackToFiles")}
        </Button>
        {canDownload && (
          <Button size="sm" onClick={() => void download()}>
            <DownloadIcon className="size-4" />
            {t("Files.Actions.Download")}
          </Button>
        )}
      </div>
    </EmptyState>
  );
}
