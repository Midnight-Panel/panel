import { extensionsApi } from "@/api/extensions";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useT } from "@/i18n";
import { FileIcon, LinkIcon, UploadIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";

interface InstallModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function InstallModal({ open, onOpenChange }: InstallModalProps) {
  const { t } = useT();
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<"file" | "link">("file");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [url, setUrl] = useState("");
  const [busy, setBusy] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleUploadFile = async () => {
    if (!selectedFile) return;
    setBusy(true);
    try {
      const res = await extensionsApi.uploadPackage(selectedFile);
      await queryClient.invalidateQueries({ queryKey: ["admin", "extensions"] });
      notifySuccess(`Erweiterung "${res.name}" erfolgreich aus Datei installiert!`);
      setSelectedFile(null);
      onOpenChange(false);
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  const handleInstallFromUrl = async () => {
    const trimmed = url.trim();
    if (!trimmed) return;
    setBusy(true);
    try {
      const res = await extensionsApi.installFromUrl(trimmed);
      await queryClient.invalidateQueries({ queryKey: ["admin", "extensions"] });
      notifySuccess(`Erweiterung "${res.name}" erfolgreich per Link installiert!`);
      setUrl("");
      onOpenChange(false);
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <div className="flex items-center gap-2">
            <div className="flex size-9 items-center justify-center rounded-lg bg-brand-500/10 text-brand-400">
              <UploadIcon className="size-5" />
            </div>
            <div>
              <DialogTitle>{t("Extensions.Install")}</DialogTitle>
              <DialogDescription>
                Installiere ein Erweiterungspaket direkt als Datei oder über einen Download-Link.
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        {/* Tab Switcher: Nur Datei oder Link */}
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
            Datei-Upload (File)
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
            Per Link installieren (URL)
          </button>
        </div>

        {activeTab === "file" ? (
          <div className="space-y-4 py-2">
            <input
              type="file"
              ref={fileInputRef}
              accept=".zip,.midnight-ext,application/zip"
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) setSelectedFile(file);
              }}
            />

            <div
              onClick={() => fileInputRef.current?.click()}
              className="group flex cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50/50 p-8 text-center transition-all hover:border-brand-400 hover:bg-gray-100/50 dark:border-gray-800 dark:bg-white/2 dark:hover:border-brand-500/40"
            >
              <div className="mb-3 flex size-12 items-center justify-center rounded-full bg-brand-500/10 text-brand-400 transition-transform group-hover:scale-105">
                <FileIcon className="size-6" />
              </div>
              <h4 className="text-sm font-semibold text-gray-800 dark:text-white/90">
                {selectedFile ? selectedFile.name : "Klicke hier, um eine Datei auszuwählen"}
              </h4>
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                Unterstützte Formate: <code className="text-brand-400">.zip</code> oder{" "}
                <code className="text-brand-400">.midnight-ext</code>
              </p>
              {selectedFile && (
                <div className="mt-3 flex items-center gap-2 rounded-lg bg-emerald-500/10 px-3 py-1 text-xs font-medium text-emerald-500">
                  <span className="size-1.5 rounded-full bg-emerald-500" />
                  Bereit: {(selectedFile.size / 1024).toFixed(1)} KB
                </div>
              )}
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
                {t("Common.Cancel")}
              </Button>
              <Button onClick={handleUploadFile} disabled={!selectedFile || busy}>
                {busy ? "Installiere Datei…" : "Datei installieren"}
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <div className="space-y-4 py-2">
            <div className="space-y-2">
              <Label className="text-xs font-semibold">Download-Link (URL) *</Label>
              <div className="relative">
                <LinkIcon className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-gray-400" />
                <Input
                  value={url}
                  onChange={(e) => setUrl(e.target.value)}
                  placeholder="https://github.com/user/extension/releases/download/v1.0.0/extension.zip"
                  className="pl-9 font-mono text-xs"
                />
              </div>
              <p className="text-[11px] text-gray-500 dark:text-gray-400">
                Gib eine direkte Download-URL zu einer Erweiterungsdatei an (z.B. GitHub Release Asset oder Web-URL).
              </p>
            </div>

            <div className="rounded-xl border border-gray-100 bg-gray-50/50 p-3.5 text-xs text-gray-600 dark:border-white/5 dark:bg-white/2 dark:text-gray-300">
              <span className="font-semibold text-gray-800 dark:text-white/90">
                Automatischer Download:
              </span>{" "}
              Das Panel lädt das Paket im Hintergrund herunter, prüft die Integrität und entpackt alle
              benötigten Dateien automatisch in dein System.
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
                {t("Common.Cancel")}
              </Button>
              <Button
                onClick={handleInstallFromUrl}
                disabled={!url.trim() || busy}
              >
                {busy ? "Lade herunter & installiere…" : "Per Link installieren"}
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
