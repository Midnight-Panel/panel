import { shardsApi } from "@/api/shards";
import type { ShardCollection } from "@/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import { CheckCircleIcon, SparkIcon, UploadIcon } from "@/icons";
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
  const [content, setContent] = useState("");
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

    const reader = new FileReader();
    reader.onload = (event) => {
      const text = event.target?.result as string;
      setContent(text || "");
    };
    reader.readAsText(file);
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
      <DialogContent className="sm:max-w-xl bg-[#0F172A] border-white/10 text-white">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 text-xl font-bold">
            <UploadIcon className="size-5 text-indigo-400" />
            {t("Shards.ImportTitle")}
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {/* File Upload / Drag Drop */}
          <div className="relative flex flex-col items-center justify-center rounded-xl border border-dashed border-white/15 bg-slate-900/40 p-6 text-center hover:border-indigo-500/50 transition-colors">
            <UploadIcon className="size-8 text-indigo-400 mb-2 opacity-80" />
            <p className="text-sm font-medium text-slate-300">
              Click here to select a JSON file or paste the code below.
            </p>
            <p className="text-xs text-slate-500 mt-1">Supports Midnight Shard (v1) &amp; PTDL templates</p>
            <input
              id={fileInputId}
              type="file"
              accept=".json,application/json"
              onChange={handleFileUpload}
              className="absolute inset-0 cursor-pointer opacity-0"
              aria-label="Select Shard JSON file"
            />
          </div>

          {/* Target Collection Selection */}
          <div>
            <Label className="text-slate-300">{t("Shards.General.Collection")} (optional)</Label>
            <select
              value={targetCollectionId ? String(targetCollectionId) : ""}
              onChange={(e: React.ChangeEvent<HTMLSelectElement>) =>
                setTargetCollectionId(e.target.value ? Number(e.target.value) : undefined)
              }
              className="mt-1.5 flex h-10 w-full rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="">Automatic (from template or &quot;Imported&quot;)</option>
              {collections.map((c: ShardCollection) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>

          {/* JSON Textarea */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <Label className="text-slate-300">JSON Content</Label>
              {detectedFormat === "midnight" && (
                <Badge size="sm" variant="light" color="primary">
                  <CheckCircleIcon className="size-3 mr-1" /> Midnight Shard v1
                </Badge>
              )}
              {detectedFormat === "legacy" && (
                <Badge size="sm" variant="light" color="warning">
                  <SparkIcon className="size-3 mr-1" /> Legacy PTDL (Auto-conversion)
                </Badge>
              )}
            </div>
            <Textarea
              value={content}
              onChange={(e) => setContent(e.target.value)}
              placeholder='{"formatVersion": "midnight_shard_v1", ...}'
              className="h-36 font-mono text-xs bg-slate-900 border-white/10 text-slate-200 resize-none"
            />
          </div>

          {/* Preview of Parsed Data */}
          {parsedName && (
            <div className="rounded-xl border border-white/5 bg-slate-900/80 p-3.5 text-xs space-y-1">
              <div className="flex items-center justify-between text-slate-300">
                <span className="font-semibold text-white">{parsedName}</span>
                <span className="text-slate-400">{parsedVariablesCount} Variables</span>
              </div>
              {parsedAuthor && <p className="text-slate-500">Author: {parsedAuthor}</p>}
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
            className="bg-indigo-600 hover:bg-indigo-500"
          >
            {busy ? "Importing..." : t("Shards.Import")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
