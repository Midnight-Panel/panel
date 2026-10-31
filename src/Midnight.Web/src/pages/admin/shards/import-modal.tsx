import { shardsApi } from "@/api/shards";
import type { ShardCollection } from "@/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import { CheckCircleIcon, FileIcon, LinkIcon, SparkIcon, UploadIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useQuery } from "@tanstack/react-query";
import { useId, useState } from "react";

interface ImportModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: () => void;
}

export function ImportModal({ open, onOpenChange, onSuccess }: ImportModalProps) {
  const { t } = useT();
  const fileInputId = useId();
  const [activeTab, setActiveTab] = useState<"file" | "link">("file");
  const [content, setContent] = useState("");
  const [url, setUrl] = useState("");
  const [selectedFileName, setSelectedFileName] = useState<string | null>(null);
  const [showRawJson, setShowRawJson] = useState(false);
  const [targetCollectionId, setTargetCollectionId] = useState<number | undefined>(undefined);
  const [busy, setBusy] = useState(false);

  const { data: collections = [] } = useQuery({
    queryKey: ["admin", "shards", "collections", "all"],
    queryFn: () => shardsApi.listAllCollections(),
    enabled: open,
  });

  // Format detection and preview
  let detectedFormat: "midnight" | "legacy" | null = null;
  let parsedName: string | null = null;
  let parsedAuthor: string | null = null;
  let parsedVariablesCount = 0;

  if (content.trim()) {
    try {
      const parsed = JSON.parse(content);
      if (parsed.formatVersion === "midnight_shard_v1") {
        detectedFormat = "midnight";
        parsedName = parsed.shard?.name;
        parsedAuthor = parsed.shard?.author;
        parsedVariablesCount = parsed.shard?.variables?.length || 0;
      } else if (parsed.meta?.version === "PTDL_v2" || parsed._comment || parsed.exported_at) {
        detectedFormat = "legacy";
        parsedName = parsed.name;
        parsedAuthor = parsed.author;
        parsedVariablesCount = parsed.variables?.length || 0;
      }
    } catch {
      // JSON incomplete
    }
  }

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setSelectedFileName(file.name);
    const reader = new FileReader();
    reader.onload = (event) => {
      const text = event.target?.result as string;
      setContent(text || "");
    };
    reader.readAsText(file);
  };

  const handleFetchUrl = async () => {
    const trimmed = url.trim();
    if (!trimmed) return;
    setBusy(true);
    try {
      const res = await fetch(trimmed);
      if (!res.ok) throw new Error(`HTTP ${res.status}: Konnte Datei nicht abrufen.`);
      const text = await res.text();
      setContent(text);
      setSelectedFileName(trimmed.split("/").pop() || "shard.json");
      notifySuccess("Shard-Datei erfolgreich von URL geladen!");
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  const handleImport = async () => {
    if (!content.trim()) return;
    setBusy(true);

    try {
      const res = await shardsApi.importShard({
        targetCollectionId: targetCollectionId || null,
        content,
      });

      notifySuccess(t("Shards.ImportSuccess", res.shardName, res.detectedFormat));
      setContent("");
      setSelectedFileName(null);
      setUrl("");
      onOpenChange(false);
      onSuccess?.();
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <UploadIcon className="size-5 text-brand-500" />
            {t("Shards.ImportTitle")}
          </DialogTitle>
        </DialogHeader>

        {/* Tab Switcher: Datei vs Link */}
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
            Datei (File)
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
            Per Link (URL)
          </button>
        </div>

        <div className="space-y-4 py-2">
          {activeTab === "file" ? (
            <div className="relative flex flex-col items-center justify-center rounded-xl border border-dashed border-gray-300 bg-gray-50/70 p-6 text-center hover:border-brand-500/50 transition-colors dark:border-gray-700 dark:bg-gray-900/40">
              <UploadIcon className="size-8 text-brand-500 mb-2 opacity-80" />
              <p className="text-sm font-medium text-gray-700 dark:text-gray-300">
                {selectedFileName ? selectedFileName : "Klicke hier, um eine Shard-Datei (.json) auszuwählen"}
              </p>
              <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                Unterstützt Midnight Shard (v1) &amp; Pterodactyl Eggs
              </p>
              <input
                id={fileInputId}
                type="file"
                accept=".json,application/json"
                onChange={handleFileUpload}
                className="absolute inset-0 cursor-pointer opacity-0"
                aria-label="Select Shard JSON file"
              />
            </div>
          ) : (
            <div className="space-y-2">
              <Label className="text-xs font-semibold">Download-Link (URL)</Label>
              <div className="flex gap-2">
                <div className="relative flex-1">
                  <LinkIcon className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-gray-400" />
                  <Input
                    value={url}
                    onChange={(e) => setUrl(e.target.value)}
                    placeholder="https://raw.githubusercontent.com/.../shard.json"
                    className="pl-9 font-mono text-xs"
                  />
                </div>
                <Button type="button" variant="outline" onClick={handleFetchUrl} disabled={!url.trim() || busy}>
                  {busy ? "Lädt…" : "Laden"}
                </Button>
              </div>
              <p className="text-[11px] text-gray-500 dark:text-gray-400">
                Direkter GitHub Raw Link oder Download-URL zu einer Shard- oder Egg-Datei.
              </p>
            </div>
          )}

          {/* Target Collection Selection */}
          <div>
            <Label>{t("Shards.General.Collection")} (optional)</Label>
            <select
              value={targetCollectionId ? String(targetCollectionId) : ""}
              onChange={(e: React.ChangeEvent<HTMLSelectElement>) =>
                setTargetCollectionId(e.target.value ? Number(e.target.value) : undefined)
              }
              className="mt-1.5 flex h-11 w-full rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
            >
              <option value="">Automatisch (aus Vorlage oder &quot;Imported&quot;)</option>
              {collections.map((c: ShardCollection) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>

          {/* Preview of Parsed Data */}
          {parsedName && (
            <div className="rounded-xl border border-gray-200 bg-gray-50/80 p-3.5 text-xs space-y-1.5 dark:border-gray-800 dark:bg-gray-900/60">
              <div className="flex items-center justify-between">
                <span className="font-semibold text-gray-800 dark:text-white/90">{parsedName}</span>
                {detectedFormat === "midnight" && (
                  <Badge size="sm" variant="light" color="primary">
                    <CheckCircleIcon className="size-3 mr-1" /> Midnight Shard v1
                  </Badge>
                )}
                {detectedFormat === "legacy" && (
                  <Badge size="sm" variant="light" color="warning">
                    <SparkIcon className="size-3 mr-1" /> Legacy PTDL
                  </Badge>
                )}
              </div>
              <div className="flex items-center justify-between text-gray-500 dark:text-gray-400">
                <span>Autor: {parsedAuthor || "Unbekannt"}</span>
                <span>{parsedVariablesCount} Variablen</span>
              </div>
            </div>
          )}

          {/* Optional Code Preview Toggle */}
          {content && (
            <div>
              <button
                type="button"
                onClick={() => setShowRawJson(!showRawJson)}
                className="text-xs text-brand-500 hover:underline dark:text-brand-400"
              >
                {showRawJson ? "Rohdaten ausblenden" : "Rohdaten anzeigen"}
              </button>
              {showRawJson && (
                <Textarea
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  className="mt-1.5 h-28 font-mono text-xs resize-none"
                />
              )}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            {t("Common.Cancel")}
          </Button>
          <Button
            type="button"
            onClick={handleImport}
            disabled={busy || !content.trim()}
          >
            {busy ? "Importiere..." : t("Shards.Import")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
