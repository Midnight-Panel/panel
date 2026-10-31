import { shardsApi } from "@/api/shards";
import type { ShardBatchImportItemResult, ShardCollection } from "@/api/types";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import {
  FileIcon,
  LinkIcon,
  OctagonAlertIcon,
  TrashBinIcon,
  UploadIcon,
} from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useQuery } from "@tanstack/react-query";
import { useId, useRef, useState } from "react";

interface ImportModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: () => void;
}

interface StagedFile {
  id: string;
  name: string;
  size: number;
  file?: File;
  content?: string;
  parsedName?: string;
}

export function ImportModal({ open, onOpenChange, onSuccess }: ImportModalProps) {
  const { t } = useT();
  const fileInputId = useId();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [activeTab, setActiveTab] = useState<"file" | "link" | "text">("file");

  const [stagedFiles, setStagedFiles] = useState<StagedFile[]>([]);
  const [urlInput, setUrlInput] = useState("");
  const [textInput, setTextInput] = useState("");
  const [targetCollectionId, setTargetCollectionId] = useState<number | undefined>(undefined);
  const [busy, setBusy] = useState(false);
  const [importSummary, setImportSummary] = useState<{
    total: number;
    succeeded: number;
    failed: number;
    results: ShardBatchImportItemResult[];
  } | null>(null);

  const { data: collections = [] } = useQuery({
    queryKey: ["admin", "shards", "collections", "all"],
    queryFn: () => shardsApi.listAllCollections(),
    enabled: open,
  });

  const handleFilesSelected = async (fileList: FileList | null) => {
    if (!fileList || fileList.length === 0) return;

    const newFiles: StagedFile[] = [];

    for (let i = 0; i < fileList.length; i++) {
      const file = fileList[i];
      let parsedName: string | undefined;

      if (file.name.endsWith(".json")) {
        try {
          const text = await file.text();
          const json = JSON.parse(text);
          parsedName = json.shard?.name || json.name;
        } catch {
          // ignore parsing error at stage
        }
      }

      newFiles.push({
        id: `${file.name}-${file.size}-${Date.now()}-${i}`,
        name: file.name,
        size: file.size,
        file,
        parsedName,
      });
    }

    setStagedFiles((prev) => [...prev, ...newFiles]);
    setImportSummary(null);

    // Reset input
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  const handleFetchUrl = async () => {
    const urls = urlInput
      .split("\n")
      .map((u) => u.trim())
      .filter((u) => u.length > 0);

    if (urls.length === 0) return;
    setBusy(true);

    try {
      const addedFiles: StagedFile[] = [];

      for (let i = 0; i < urls.length; i++) {
        const u = urls[i];
        try {
          const res = await fetch(u);
          if (!res.ok) throw new Error(`HTTP ${res.status}`);
          const text = await res.text();
          let parsedName: string | undefined;

          try {
            const json = JSON.parse(text);
            parsedName = json.shard?.name || json.name;
          } catch {
            // ignore
          }

          const fileName = u.split("/").pop()?.split("?")[0] || `shard-${i + 1}.json`;

          addedFiles.push({
            id: `url-${Date.now()}-${i}`,
            name: fileName,
            size: new Blob([text]).size,
            content: text,
            parsedName,
          });
        } catch (err: unknown) {
          notifyError(`Fehler beim Laden von ${u}: ${err instanceof Error ? err.message : String(err)}`);
        }
      }

      if (addedFiles.length > 0) {
        setStagedFiles((prev) => [...prev, ...addedFiles]);
        setUrlInput("");
        notifySuccess(`${addedFiles.length} Shard(s) aus URL geladen.`);
      }
    } finally {
      setBusy(false);
    }
  };

  const handleAddTextContent = () => {
    const trimmed = textInput.trim();
    if (!trimmed) return;

    let parsedName: string | undefined;
    try {
      const json = JSON.parse(trimmed);
      parsedName = json.shard?.name || json.name;
    } catch {
      notifyError("Der eingegebene Text ist kein gültiges JSON.");
      return;
    }

    setStagedFiles((prev) => [
      ...prev,
      {
        id: `text-${Date.now()}`,
        name: parsedName ? `${parsedName}.json` : "pasted-shard.json",
        size: new Blob([trimmed]).size,
        content: trimmed,
        parsedName,
      },
    ]);
    setTextInput("");
    setActiveTab("file");
  };

  const removeStagedFile = (id: string) => {
    setStagedFiles((prev) => prev.filter((f) => f.id !== id));
  };

  const clearAllStaged = () => {
    setStagedFiles([]);
    setImportSummary(null);
  };

  const handleExecuteImport = async () => {
    if (stagedFiles.length === 0) return;
    setBusy(true);

    try {
      // Prepare items for batch import
      const itemsToUpload: Array<{ fileName: string; content: string }> = [];
      const rawFilesToUpload: File[] = [];

      for (const staged of stagedFiles) {
        if (staged.file) {
          // If it's a zip or we can pass File objects
          rawFilesToUpload.push(staged.file);
        } else if (staged.content) {
          itemsToUpload.push({
            fileName: staged.name,
            content: staged.content,
          });
        }
      }

      let batchRes;

      if (rawFilesToUpload.length > 0) {
        batchRes = await shardsApi.uploadShardFiles(rawFilesToUpload, targetCollectionId);
      } else {
        batchRes = await shardsApi.importShardsBatch({
          targetCollectionId,
          items: itemsToUpload,
        });
      }

      setImportSummary({
        total: batchRes.totalCount,
        succeeded: batchRes.succeededCount,
        failed: batchRes.failedCount,
        results: batchRes.results,
      });

      if (batchRes.succeededCount > 0) {
        notifySuccess(
          `${batchRes.succeededCount} von ${batchRes.totalCount} Shard(s) erfolgreich importiert!`,
        );
        onSuccess?.();
      }

      if (batchRes.failedCount === 0) {
        setStagedFiles([]);
        onOpenChange(false);
      }
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <div className="flex items-center gap-2">
            <div className="flex size-9 items-center justify-center rounded-lg bg-brand-500/10 text-brand-400">
              <UploadIcon className="size-5" />
            </div>
            <div>
              <DialogTitle>{t("Shards.ImportTitle")}</DialogTitle>
              <DialogDescription>
                Lade mehrere Shards gleichzeitig als Dateien (.json oder .zip) oder per Download-Link hoch.
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        {/* Tab Switcher */}
        <div className="flex items-center rounded-xl border border-gray-200 bg-gray-50/50 p-1 text-xs dark:border-gray-800 dark:bg-white/2">
          <button
            type="button"
            onClick={() => setActiveTab("file")}
            className={`flex flex-1 items-center justify-center gap-2 rounded-lg py-2 font-medium transition-all ${
              activeTab === "file"
                ? "bg-brand-500 text-white shadow-xs"
                : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
            }`}
          >
            <FileIcon className="size-4" />
            Dateien (Multi-Upload / ZIP)
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("link")}
            className={`flex flex-1 items-center justify-center gap-2 rounded-lg py-2 font-medium transition-all ${
              activeTab === "link"
                ? "bg-brand-500 text-white shadow-xs"
                : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
            }`}
          >
            <LinkIcon className="size-4" />
            Per Link / URL
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("text")}
            className={`flex flex-1 items-center justify-center gap-2 rounded-lg py-2 font-medium transition-all ${
              activeTab === "text"
                ? "bg-brand-500 text-white shadow-xs"
                : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
            }`}
          >
            Direkt einfügen
          </button>
        </div>

        <div className="space-y-4 py-2">
          {activeTab === "file" && (
            <div>
              <input
                id={fileInputId}
                ref={fileInputRef}
                type="file"
                multiple
                accept=".json,application/json,.zip,application/zip"
                onChange={(e) => handleFilesSelected(e.target.files)}
                className="hidden"
              />

              <div
                onClick={() => fileInputRef.current?.click()}
                onDragOver={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                }}
                onDrop={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                  handleFilesSelected(e.dataTransfer.files);
                }}
                className="group flex cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50/50 p-8 text-center transition-all hover:border-brand-400 hover:bg-gray-100/50 dark:border-gray-800 dark:bg-white/2 dark:hover:border-brand-500/40"
              >
                <div className="mb-3 flex size-12 items-center justify-center rounded-full bg-brand-500/10 text-brand-400 transition-transform group-hover:scale-105">
                  <UploadIcon className="size-6" />
                </div>
                <h4 className="text-sm font-semibold text-gray-800 dark:text-white/90">
                  Dateien hier ablegen oder klicken zum Auswählen
                </h4>
                <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                  Wähle mehrere <code className="text-brand-400">.json</code> Shard-Dateien oder ein{" "}
                  <code className="text-brand-400">.zip</code> Archiv mit Shards aus.
                </p>
                <div className="mt-3 flex items-center gap-2 text-[11px] text-gray-400">
                  <span>Midnight Shards (v1) &amp; Pterodactyl Eggs werden automatisch erkannt</span>
                </div>
              </div>
            </div>
          )}

          {activeTab === "link" && (
            <div className="space-y-2">
              <Label className="text-xs font-semibold">Download-URLs (eine pro Zeile)</Label>
              <Textarea
                value={urlInput}
                onChange={(e) => setUrlInput(e.target.value)}
                placeholder="https://raw.githubusercontent.com/.../shard1.json&#10;https://raw.githubusercontent.com/.../shard2.json"
                className="h-28 font-mono text-xs"
              />
              <div className="flex justify-end">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={handleFetchUrl}
                  disabled={!urlInput.trim() || busy}
                >
                  <LinkIcon className="mr-1.5 size-3.5" />
                  {busy ? "Lade…" : "Shards laden & zur Liste hinzufügen"}
                </Button>
              </div>
            </div>
          )}

          {activeTab === "text" && (
            <div className="space-y-2">
              <Label className="text-xs font-semibold">JSON-Code einfügen</Label>
              <Textarea
                value={textInput}
                onChange={(e) => setTextInput(e.target.value)}
                placeholder='{"formatVersion": "midnight_shard_v1", ...}'
                className="h-32 font-mono text-xs"
              />
              <div className="flex justify-end">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={handleAddTextContent}
                  disabled={!textInput.trim()}
                >
                  Zur Liste hinzufügen
                </Button>
              </div>
            </div>
          )}

          {/* Target Collection Selection */}
          <div>
            <Label className="text-xs">Ziel-Sammlung (optional)</Label>
            <select
              value={targetCollectionId ? String(targetCollectionId) : ""}
              onChange={(e) =>
                setTargetCollectionId(e.target.value ? Number(e.target.value) : undefined)
              }
              className="mt-1 flex h-11 w-full rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
            >
              <option value="">Automatisch (aus Shard-Vorlage oder &quot;Imported&quot;)</option>
              {collections.map((c: ShardCollection) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>

          {/* Staged Files List */}
          {stagedFiles.length > 0 && (
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold text-gray-700 dark:text-gray-300">
                  Bereit zum Importieren ({stagedFiles.length} Shard{stagedFiles.length > 1 ? "s" : ""}):
                </span>
                <button
                  type="button"
                  onClick={clearAllStaged}
                  className="text-xs text-red-500 hover:underline dark:text-red-400"
                >
                  Alle entfernen
                </button>
              </div>

              <div className="max-h-48 space-y-1.5 overflow-y-auto pr-1">
                {stagedFiles.map((file) => (
                  <div
                    key={file.id}
                    className="flex items-center justify-between rounded-xl border border-gray-100 bg-gray-50/70 p-2.5 text-xs dark:border-white/5 dark:bg-white/2"
                  >
                    <div className="flex items-center gap-2.5 overflow-hidden">
                      <FileIcon className="size-4 shrink-0 text-brand-500" />
                      <div className="truncate">
                        <span className="font-medium text-gray-800 dark:text-gray-200">
                          {file.parsedName || file.name}
                        </span>
                        {file.parsedName && file.parsedName !== file.name && (
                          <span className="ml-1.5 font-mono text-[11px] text-gray-400">
                            ({file.name})
                          </span>
                        )}
                      </div>
                    </div>

                    <div className="flex items-center gap-3">
                      <span className="font-mono text-[11px] text-gray-400">
                        {(file.size / 1024).toFixed(1)} KB
                      </span>
                      <button
                        type="button"
                        onClick={() => removeStagedFile(file.id)}
                        className="text-gray-400 hover:text-red-500 dark:hover:text-red-400"
                        title="Entfernen"
                      >
                        <TrashBinIcon className="size-3.5" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Results Summary if any errors occurred */}
          {importSummary && importSummary.failed > 0 && (
            <div className="rounded-xl border border-red-200 bg-red-50/50 p-3.5 text-xs dark:border-red-500/20 dark:bg-red-500/5">
              <div className="flex items-center gap-2 font-semibold text-red-600 dark:text-red-400">
                <OctagonAlertIcon className="size-4" />
                <span>
                  {importSummary.failed} von {importSummary.total} Shard(s) konnten nicht importiert werden:
                </span>
              </div>
              <ul className="mt-2 space-y-1 text-gray-600 dark:text-gray-300">
                {importSummary.results
                  .filter((r) => !r.success)
                  .map((r, i) => (
                    <li key={i} className="flex items-start gap-1.5">
                      <span className="font-mono font-medium text-gray-800 dark:text-white">
                        {r.fileName}:
                      </span>
                      <span>{r.errorMessage}</span>
                    </li>
                  ))}
              </ul>
            </div>
          )}
        </div>

        <DialogFooter className="gap-2 sm:gap-0">
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={busy}
          >
            {t("Common.Cancel")}
          </Button>
          <Button
            type="button"
            onClick={handleExecuteImport}
            disabled={busy || stagedFiles.length === 0}
          >
            {busy
              ? "Importiere…"
              : stagedFiles.length > 1
              ? `${stagedFiles.length} Shards importieren`
              : "Shard importieren"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
