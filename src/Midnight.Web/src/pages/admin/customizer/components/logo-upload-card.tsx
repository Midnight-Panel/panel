import { useState } from "react";
import { useDropzone } from "react-dropzone";
import { toast } from "sonner";
import { brandingApi } from "@/api/branding";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { UploadIcon, TrashBinIcon, CheckCircleIcon, SparkIcon } from "@/icons";

interface LogoUploadCardProps {
  type: "light" | "dark" | "favicon";
  title: string;
  description: string;
  currentUrl?: string | null;
  onSuccess: () => Promise<void>;
}

export function LogoUploadCard({
  type,
  title,
  description,
  currentUrl,
  onSuccess,
}: LogoUploadCardProps) {
  const [loading, setLoading] = useState(false);

  const isFavicon = type === "favicon";
  const acceptedTypes = isFavicon
    ? { "image/*": [".png", ".ico", ".svg", ".webp"] }
    : { "image/*": [".png", ".webp", ".svg"] };

  const handleUpload = async (file: File) => {
    if (file.size > 2 * 1024 * 1024) {
      toast.error("Die Datei darf maximal 2 MB groß sein.");
      return;
    }

    setLoading(true);
    try {
      if (type === "favicon") {
        await brandingApi.uploadFavicon(file);
        toast.success("Favicon erfolgreich hochgeladen!");
      } else {
        await brandingApi.uploadLogo(type, file);
        toast.success(
          type === "light"
            ? "Logo für hellen Modus erfolgreich hochgeladen!"
            : "Logo für dunklen Modus erfolgreich hochgeladen!",
        );
      }
      await onSuccess();
    } catch (err: any) {
      toast.error(err?.detail || err?.message || "Fehler beim Hochladen der Bilddatei.");
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async () => {
    setLoading(true);
    try {
      if (type === "favicon") {
        await brandingApi.deleteFavicon();
        toast.success("Favicon auf Standard zurückgesetzt.");
      } else {
        await brandingApi.deleteLogo(type);
        toast.success(
          type === "light"
            ? "Helles Logo auf Standard zurückgesetzt."
            : "Dunkles Logo auf Standard zurückgesetzt.",
        );
      }
      await onSuccess();
    } catch (err: any) {
      toast.error(err?.detail || err?.message || "Fehler beim Zurücksetzen.");
    } finally {
      setLoading(false);
    }
  };

  const { getRootProps, getInputProps, isDragActive, isDragReject, open } = useDropzone({
    onDrop: (acceptedFiles) => {
      if (acceptedFiles.length > 0) {
        handleUpload(acceptedFiles[0]);
      }
    },
    accept: acceptedTypes,
    maxFiles: 1,
    noClick: !!currentUrl, // Wenn Bild vorhanden ist, wird gezielt per Button geöffnet
  });

  const previewBgClass =
    type === "light"
      ? "bg-slate-100 border-slate-200"
      : type === "dark"
        ? "bg-slate-950 border-slate-800"
        : "bg-slate-900 border-slate-800";

  return (
    <div
      {...getRootProps()}
      className={`relative group rounded-xl border transition-all duration-200 p-5 ${
        isDragActive
          ? "border-brand-500 bg-brand-500/5 ring-2 ring-brand-500/20"
          : isDragReject
            ? "border-red-500 bg-red-500/5 ring-2 ring-red-500/20"
            : "border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02] hover:border-gray-300 dark:hover:border-gray-700"
      }`}
    >
      <input {...getInputProps()} />

      <div className="flex items-start justify-between gap-4 mb-4">
        <div>
          <div className="flex items-center gap-2">
            <h4 className="font-outfit font-medium text-gray-900 dark:text-white text-base">
              {title}
            </h4>
            {currentUrl ? (
              <Badge variant="light" color="primary" size="sm">
                Individuell
              </Badge>
            ) : (
              <Badge variant="light" color="light" size="sm">
                Standard
              </Badge>
            )}
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{description}</p>
        </div>

        {currentUrl && (
          <div className="flex items-center gap-1.5">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={open}
              disabled={loading}
              className="h-8 text-xs font-normal"
            >
              <UploadIcon className="size-3.5 mr-1" />
              Ersetzen
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={handleDelete}
              disabled={loading}
              className="h-8 text-xs text-red-500 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-950/30"
              title="Auf Standard zurücksetzen"
            >
              <TrashBinIcon className="size-3.5 mr-1" />
              Entfernen
            </Button>
          </div>
        )}
      </div>

      {/* Preview & Drop Area */}
      {currentUrl ? (
        <div
          className={`flex items-center justify-center p-6 rounded-lg border ${previewBgClass} min-h-[120px] relative overflow-hidden`}
        >
          <img
            src={currentUrl}
            alt={title}
            className={`max-h-16 w-auto object-contain transition-transform duration-200 group-hover:scale-105 ${
              isFavicon ? "h-10 w-10" : "max-w-[200px]"
            }`}
          />
          <div className="absolute bottom-2 right-2 flex items-center gap-1 text-[11px] text-gray-400 bg-black/40 px-2 py-0.5 rounded backdrop-blur-xs">
            <CheckCircleIcon className="size-3 text-emerald-400" />
            <span>Aktiv</span>
          </div>
        </div>
      ) : (
        <div
          onClick={open}
          className="flex flex-col items-center justify-center p-6 rounded-lg border-2 border-dashed border-gray-200 dark:border-gray-800 bg-gray-50/50 dark:bg-white/[0.01] hover:bg-gray-100/50 dark:hover:bg-white/[0.03] cursor-pointer transition-colors min-h-[120px]"
        >
          <div className="size-10 rounded-full bg-brand-500/10 flex items-center justify-center text-brand-500 mb-2">
            <UploadIcon className="size-5" />
          </div>
          <span className="text-xs font-medium text-gray-700 dark:text-gray-300">
            {isDragActive ? "Datei hier loslassen..." : "Klicken oder Datei hierher ziehen"}
          </span>
          <span className="text-[11px] text-gray-400 mt-1">
            {isFavicon ? "PNG, ICO, SVG, WebP (max. 2 MB)" : "PNG, SVG, WebP (max. 2 MB)"}
          </span>
        </div>
      )}

      {/* Help info footer */}
      <div className="mt-3 flex items-center justify-between text-[11px] text-gray-500 dark:text-gray-400">
        <span className="flex items-center gap-1">
          <SparkIcon className="size-3 text-brand-400" />
          {type === "light"
            ? "Optimal für helle Oberflächen & E-Mails"
            : type === "dark"
              ? "Optimal für dunkles Dashboard-Thema"
              : "Wird im Browser-Tab dargestellt"}
        </span>
        <span>Max. 2 MB</span>
      </div>
    </div>
  );
}
