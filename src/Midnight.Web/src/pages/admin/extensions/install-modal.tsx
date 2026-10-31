import { extensionsApi } from "@/api/extensions";
import type { ExtensionInstallRequest } from "@/api/types";
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
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import { PlusIcon, UploadIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { useForm } from "react-hook-form";

interface InstallModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  categories: string[];
}

export function InstallModal({ open, onOpenChange, categories }: InstallModalProps) {
  const { t } = useT();
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<"upload" | "manual">("upload");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ExtensionInstallRequest>({
    defaultValues: {
      identifier: "",
      name: "",
      description: "",
      category: "Tools",
      version: "1.0.0",
      author: "Community",
      websiteUrl: "",
      icon: "plugin",
    },
  });

  const handleUpload = async () => {
    if (!selectedFile) return;
    setBusy(true);
    try {
      await extensionsApi.uploadPackage(selectedFile);
      await queryClient.invalidateQueries({ queryKey: ["admin", "extensions"] });
      notifySuccess(t("Extensions.InstalledSuccess"));
      setSelectedFile(null);
      onOpenChange(false);
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  const handleManualInstall = async (values: ExtensionInstallRequest) => {
    setBusy(true);
    try {
      await extensionsApi.install(values);
      await queryClient.invalidateQueries({ queryKey: ["admin", "extensions"] });
      notifySuccess(t("Extensions.InstalledSuccess"));
      reset();
      onOpenChange(false);
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <div className="flex items-center gap-2">
            <div className="flex size-9 items-center justify-center rounded-lg bg-brand-500/10 text-brand-400">
              <PlusIcon className="size-5" />
            </div>
            <div>
              <DialogTitle>{t("Extensions.Install")}</DialogTitle>
              <DialogDescription>
                Installiere Erweiterungen über ein ZIP-Archiv oder registriere ein neues Modul manuell.
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        {/* Tab Switcher */}
        <div className="flex items-center rounded-lg border border-gray-200 p-1 text-xs dark:border-gray-800">
          <button
            type="button"
            onClick={() => setActiveTab("upload")}
            className={`flex flex-1 items-center justify-center gap-1.5 rounded-md py-1.5 font-medium transition-colors ${
              activeTab === "upload"
                ? "bg-brand-500 text-white shadow-xs"
                : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
            }`}
          >
            <UploadIcon className="size-3.5" />
            Paket-Upload (.zip)
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("manual")}
            className={`flex flex-1 items-center justify-center gap-1.5 rounded-md py-1.5 font-medium transition-colors ${
              activeTab === "manual"
                ? "bg-brand-500 text-white shadow-xs"
                : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
            }`}
          >
            <PlusIcon className="size-3.5" />
            Manuell registrieren
          </button>
        </div>

        {activeTab === "upload" ? (
          <div className="space-y-4 py-2">
            <input
              type="file"
              ref={fileInputRef}
              accept=".zip"
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) setSelectedFile(file);
              }}
            />

            <div
              onClick={() => fileInputRef.current?.click()}
              className="flex cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50/50 p-8 text-center transition-colors hover:border-brand-400 dark:border-gray-800 dark:bg-white/2 dark:hover:border-brand-500/40"
            >
              <div className="mb-3 flex size-12 items-center justify-center rounded-full bg-brand-500/10 text-brand-400">
                <UploadIcon className="size-6" />
              </div>
              <h4 className="text-sm font-semibold text-gray-800 dark:text-white/90">
                {selectedFile ? selectedFile.name : "Klicke zum Auswählen des Archivs"}
              </h4>
              <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                Unterstützt werden signierte oder verpackte .zip Dateien mit einem{" "}
                <code className="text-brand-400">extension.json</code> Manifest.
              </p>
              {selectedFile && (
                <div className="mt-3 text-xs font-medium text-emerald-500">
                  Ausgewählt: {(selectedFile.size / 1024).toFixed(1)} KB
                </div>
              )}
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                {t("Common.Cancel")}
              </Button>
              <Button onClick={handleUpload} disabled={!selectedFile || busy}>
                {busy ? "Wird installiert…" : "Paket installieren"}
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <form onSubmit={handleSubmit(handleManualInstall)} className="space-y-3.5 py-2">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">Identifier *</Label>
                <Input
                  {...register("identifier", {
                    required: "Identifier ist erforderlich",
                    pattern: {
                      value: /^[a-z0-9-_]+$/,
                      message: "Nur Kleinbuchstaben, Ziffern und Bindestriche",
                    },
                  })}
                  placeholder="z.B. discord-webhook"
                  className="font-mono text-xs"
                />
                {errors.identifier && (
                  <p className="text-[11px] text-red-500">{errors.identifier.message}</p>
                )}
              </div>

              <div className="space-y-1">
                <Label className="text-xs">Name *</Label>
                <Input
                  {...register("name", { required: "Name ist erforderlich" })}
                  placeholder="z.B. Discord Notifier"
                />
                {errors.name && <p className="text-[11px] text-red-500">{errors.name.message}</p>}
              </div>
            </div>

            <div className="space-y-1">
              <Label className="text-xs">Beschreibung</Label>
              <Textarea
                {...register("description")}
                placeholder="Kurze Zusammenfassung der Funktion…"
                className="h-20 text-xs"
              />
            </div>

            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">Kategorie</Label>
                <select
                  {...register("category")}
                  className="flex h-11 w-full rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-xs text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
                >
                  {categories.map((c) => (
                    <option key={c} value={c}>
                      {c}
                    </option>
                  ))}
                  <option value="Tools">Tools</option>
                  <option value="Integration">Integration</option>
                  <option value="Security">Security</option>
                  <option value="Administration">Administration</option>
                </select>
              </div>

              <div className="space-y-1">
                <Label className="text-xs">Version</Label>
                <Input {...register("version")} placeholder="1.0.0" className="font-mono text-xs" />
              </div>

              <div className="space-y-1">
                <Label className="text-xs">Autor</Label>
                <Input {...register("author")} placeholder="Midnightpanel" className="text-xs" />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label className="text-xs">Webseite URL</Label>
                <Input
                  {...register("websiteUrl")}
                  placeholder="https://example.com"
                  className="text-xs"
                />
              </div>

              <div className="space-y-1">
                <Label className="text-xs">Icon</Label>
                <Input {...register("icon")} placeholder="chat, shield, globe, chart" className="text-xs" />
              </div>
            </div>

            <DialogFooter className="pt-2">
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                {t("Common.Cancel")}
              </Button>
              <Button type="submit" disabled={busy}>
                {busy ? "Registriere…" : "Erweiterung anlegen"}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
