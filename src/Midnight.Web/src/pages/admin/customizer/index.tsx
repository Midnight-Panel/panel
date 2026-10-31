import { useState, useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { useDropzone } from "react-dropzone";
import { toast } from "sonner";
import { useBranding } from "@/contexts/BrandingContext";
import { brandingApi } from "@/api/branding";
import { PoweredByFooter } from "@/components/shared/powered-by-footer";
import { BrushIcon, UploadIcon, FloppyDiscIcon, RegenerateIcon } from "@/icons";

function getLuminance(hex: string): number {
  const match = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  if (!match) return 0;
  const rgb = [parseInt(match[1], 16), parseInt(match[2], 16), parseInt(match[3], 16)].map((v) => {
    v /= 255;
    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
}

function getContrast(c1: string, c2: string): number {
  const l1 = getLuminance(c1);
  const l2 = getLuminance(c2);
  return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
}

const brandingSchema = z.object({
  panelName: z.string().min(2, "Mindestens 2 Zeichen").max(64, "Maximal 64 Zeichen"),
  primaryColor: z.string().regex(/^#[0-9A-Fa-f]{6}$/, "Muss ein Hex-Code sein (#RRGGBB)"),
  accentColor: z.string().regex(/^#[0-9A-Fa-f]{6}$/, "Muss ein Hex-Code sein (#RRGGBB)"),
  loginBackgroundType: z.enum(["none", "gradient", "image"]),
  loginBackgroundValue: z.string().optional().nullable(),
  customCss: z.string().optional().nullable(),
});

type FormValues = z.infer<typeof brandingSchema>;

export default function AdminCustomizerPage() {
  const { branding, reload } = useBranding();
  const [saving, setSaving] = useState(false);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(brandingSchema),
    defaultValues: {
      panelName: branding?.panelName || "Midnight Panel",
      primaryColor: branding?.primaryColor || "#465FFF",
      accentColor: branding?.accentColor || "#7C3AED",
      loginBackgroundType: branding?.loginBackgroundType || "gradient",
      loginBackgroundValue:
        branding?.loginBackgroundValue ||
        "linear-gradient(135deg, #0F172A 0%, #1E1B4B 50%, #0F172A 100%)",
      customCss: branding?.customCss || "",
    },
  });

  useEffect(() => {
    if (branding) {
      reset({
        panelName: branding.panelName,
        primaryColor: branding.primaryColor,
        accentColor: branding.accentColor,
        loginBackgroundType: branding.loginBackgroundType,
        loginBackgroundValue: branding.loginBackgroundValue,
        customCss: branding.customCss || "",
      });
    }
  }, [branding, reset]);

  const liveValues = watch();

  const primaryOnDark = getContrast(liveValues.primaryColor, "#0F172A");
  const primaryOnWhite = getContrast(liveValues.primaryColor, "#FFFFFF");

  const onDropLogoLight = async (files: File[]) => {
    if (files.length === 0) return;
    try {
      await brandingApi.uploadLogo("light", files[0]);
      toast.success("Logo für helles Farbschema aktualisiert!");
      await reload();
    } catch (err: any) {
      toast.error(err?.detail || err?.message || "Fehler beim Upload des hellen Logos.");
    }
  };

  const onDropLogoDark = async (files: File[]) => {
    if (files.length === 0) return;
    try {
      await brandingApi.uploadLogo("dark", files[0]);
      toast.success("Logo für dunkles Farbschema aktualisiert!");
      await reload();
    } catch (err: any) {
      toast.error(err?.detail || err?.message || "Fehler beim Upload des dunklen Logos.");
    }
  };

  const onDropFavicon = async (files: File[]) => {
    if (files.length === 0) return;
    try {
      await brandingApi.uploadFavicon(files[0]);
      toast.success("Favicon erfolgreich aktualisiert!");
      await reload();
    } catch (err: any) {
      toast.error(err?.detail || err?.message || "Fehler beim Upload des Favicons.");
    }
  };

  const dropLight = useDropzone({
    onDrop: onDropLogoLight,
    accept: { "image/*": [".png", ".webp", ".svg"] },
    maxFiles: 1,
  });

  const dropDark = useDropzone({
    onDrop: onDropLogoDark,
    accept: { "image/*": [".png", ".webp", ".svg"] },
    maxFiles: 1,
  });

  const dropFavicon = useDropzone({
    onDrop: onDropFavicon,
    accept: { "image/*": [".png", ".ico", ".svg", ".webp"] },
    maxFiles: 1,
  });

  const onSubmit = async (values: FormValues) => {
    setSaving(true);
    try {
      await brandingApi.update({
        panelName: values.panelName,
        primaryColor: values.primaryColor,
        accentColor: values.accentColor,
        loginBackgroundType: values.loginBackgroundType,
        loginBackgroundValue: values.loginBackgroundValue ?? null,
        customCss: values.customCss ?? null,
      });
      toast.success("Branding-Einstellungen erfolgreich gespeichert!");
      await reload();
    } catch (err: any) {
      toast.error(err?.detail || err?.message || "Fehler beim Speichern der Branding-Einstellungen.");
    } finally {
      setSaving(false);
    }
  };

  const handleReset = async () => {
    if (!confirm("Möchtest du wirklich alle Branding-Einstellungen auf Standardwerte zurücksetzen?"))
      return;

    try {
      await brandingApi.reset();
      toast.success("Branding auf Werkseinstellungen zurückgesetzt.");
      await reload();
      reset({
        panelName: "Midnight Panel",
        primaryColor: "#465FFF",
        accentColor: "#7C3AED",
        loginBackgroundType: "gradient",
        loginBackgroundValue: "linear-gradient(135deg, #0F172A 0%, #1E1B4B 50%, #0F172A 100%)",
        customCss: "",
      });
    } catch (err: any) {
      toast.error(err?.detail || err?.message || "Fehler beim Zurücksetzen.");
    }
  };

  return (
    <div className="flex flex-col gap-6">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <BrushIcon className="size-6 text-brand-500" />
            <h1 className="text-2xl font-bold tracking-tight text-gray-900 dark:text-white">
              Customizer & Branding
            </h1>
          </div>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Passe Farben, Logos und das visuelle Erscheinungsbild von Midnight Panel an.
          </p>
        </div>
        <button
          type="button"
          onClick={handleReset}
          className="inline-flex items-center gap-1.5 rounded-lg border border-red-500/30 px-3 py-1.5 text-xs font-medium text-red-500 hover:bg-red-500/10 transition-colors"
        >
          <RegenerateIcon className="size-3.5" />
          Auf Standard zurücksetzen
        </button>
      </div>

      {/* Split-View Grid */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Linke Spalte: Formular */}
        <form
          onSubmit={handleSubmit(onSubmit)}
          className="flex flex-col gap-5 rounded-2xl border border-gray-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/60 backdrop-blur-md"
        >
          {/* Panel Name */}
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-200">
              Panel-Name
            </label>
            <input
              {...register("panelName")}
              placeholder="z. B. Mein Server Panel"
              className="w-full rounded-xl border border-gray-300 bg-gray-50 px-3.5 py-2.5 text-sm text-gray-900 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-slate-800/80 dark:text-white"
            />
            {errors.panelName && (
              <span className="mt-1 block text-xs text-red-500">{errors.panelName.message}</span>
            )}
            <p className="mt-1 text-xs text-gray-400">
              Wird im Browser-Tab, in E-Mails und in der Navigation verwendet.
            </p>
          </div>

          {/* Farbwähler mit WCAG Kontrastberechnung */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-200">
                Primärfarbe
              </label>
              <div className="flex items-center gap-2">
                <input
                  type="color"
                  value={liveValues.primaryColor || "#465FFF"}
                  onChange={(e) => setValue("primaryColor", e.target.value)}
                  className="size-10 cursor-pointer rounded-lg border border-gray-300 dark:border-white/10 bg-transparent p-0.5"
                />
                <input
                  {...register("primaryColor")}
                  className="w-full rounded-xl border border-gray-300 bg-gray-50 px-3 py-2 text-sm text-gray-900 dark:border-white/10 dark:bg-slate-800/80 dark:text-white"
                />
              </div>
              {/* WCAG Kontrastindikator */}
              <div className="mt-2 flex flex-col gap-1 text-[11px] text-gray-500 dark:text-gray-400">
                <div className="flex items-center justify-between">
                  <span>Kontrast auf Dark Mode:</span>
                  <span
                    className={`font-semibold ${
                      primaryOnDark >= 4.5
                        ? "text-emerald-500 dark:text-emerald-400"
                        : "text-amber-500 dark:text-amber-400"
                    }`}
                  >
                    {primaryOnDark.toFixed(1)}:1 {primaryOnDark >= 4.5 ? "✓ WCAG AA" : "⚠ Gering"}
                  </span>
                </div>
                <div className="flex items-center justify-between">
                  <span>Kontrast auf White:</span>
                  <span
                    className={`font-semibold ${
                      primaryOnWhite >= 4.5
                        ? "text-emerald-500 dark:text-emerald-400"
                        : "text-amber-500 dark:text-amber-400"
                    }`}
                  >
                    {primaryOnWhite.toFixed(1)}:1 {primaryOnWhite >= 4.5 ? "✓ WCAG AA" : "⚠ Gering"}
                  </span>
                </div>
              </div>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-200">
                Akzentfarbe
              </label>
              <div className="flex items-center gap-2">
                <input
                  type="color"
                  value={liveValues.accentColor || "#7C3AED"}
                  onChange={(e) => setValue("accentColor", e.target.value)}
                  className="size-10 cursor-pointer rounded-lg border border-gray-300 dark:border-white/10 bg-transparent p-0.5"
                />
                <input
                  {...register("accentColor")}
                  className="w-full rounded-xl border border-gray-300 bg-gray-50 px-3 py-2 text-sm text-gray-900 dark:border-white/10 dark:bg-slate-800/80 dark:text-white"
                />
              </div>
            </div>
          </div>

          {/* Logo Uploads */}
          <div className="flex flex-col gap-3">
            <span className="text-sm font-medium text-gray-700 dark:text-gray-200">
              Logos & Favicon
            </span>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              {/* Light Logo */}
              <div
                {...dropLight.getRootProps()}
                className="flex cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed border-gray-200 bg-gray-50/50 p-3.5 text-center transition hover:border-brand-500 dark:border-white/10 dark:bg-slate-800/40"
              >
                <input {...dropLight.getInputProps()} />
                <UploadIcon className="size-5 text-gray-400 mb-1" />
                <span className="text-xs font-semibold text-gray-700 dark:text-gray-200">
                  Helles Logo
                </span>
                <span className="text-[10px] text-gray-400 mt-0.5">PNG, SVG, WebP</span>
                {branding?.logoLightUrl && (
                  <span className="mt-1 text-[10px] text-emerald-500 font-medium">Hochgeladen ✓</span>
                )}
              </div>

              {/* Dark Logo */}
              <div
                {...dropDark.getRootProps()}
                className="flex cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed border-gray-200 bg-gray-50/50 p-3.5 text-center transition hover:border-brand-500 dark:border-white/10 dark:bg-slate-800/40"
              >
                <input {...dropDark.getInputProps()} />
                <UploadIcon className="size-5 text-gray-400 mb-1" />
                <span className="text-xs font-semibold text-gray-700 dark:text-gray-200">
                  Dunkles Logo
                </span>
                <span className="text-[10px] text-gray-400 mt-0.5">PNG, SVG, WebP</span>
                {branding?.logoDarkUrl && (
                  <span className="mt-1 text-[10px] text-emerald-500 font-medium">Hochgeladen ✓</span>
                )}
              </div>

              {/* Favicon */}
              <div
                {...dropFavicon.getRootProps()}
                className="flex cursor-pointer flex-col items-center justify-center rounded-xl border-2 border-dashed border-gray-200 bg-gray-50/50 p-3.5 text-center transition hover:border-brand-500 dark:border-white/10 dark:bg-slate-800/40"
              >
                <input {...dropFavicon.getInputProps()} />
                <UploadIcon className="size-5 text-gray-400 mb-1" />
                <span className="text-xs font-semibold text-gray-700 dark:text-gray-200">
                  Favicon
                </span>
                <span className="text-[10px] text-gray-400 mt-0.5">ICO, PNG, SVG</span>
                {branding?.faviconUrl && (
                  <span className="mt-1 text-[10px] text-emerald-500 font-medium">Hochgeladen ✓</span>
                )}
              </div>
            </div>
          </div>

          {/* Login Background */}
          <div>
            <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-200">
              Login-Seiten Hintergrund
            </label>
            <div className="grid grid-cols-3 gap-2 mb-2">
              {(["gradient", "none", "image"] as const).map((type) => (
                <button
                  key={type}
                  type="button"
                  onClick={() => setValue("loginBackgroundType", type)}
                  className={`rounded-lg py-2 text-xs font-medium border capitalize transition-colors ${
                    liveValues.loginBackgroundType === type
                      ? "border-brand-500 bg-brand-500/10 text-brand-500"
                      : "border-gray-200 dark:border-white/10 text-gray-600 dark:text-gray-300"
                  }`}
                >
                  {type}
                </button>
              ))}
            </div>
            {liveValues.loginBackgroundType === "gradient" && (
              <input
                {...register("loginBackgroundValue")}
                placeholder="linear-gradient(...)"
                className="w-full rounded-xl border border-gray-300 bg-gray-50 px-3.5 py-2 text-xs font-mono text-gray-900 dark:border-white/10 dark:bg-slate-800/80 dark:text-white"
              />
            )}
          </div>

          {/* Custom CSS */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-200">
                Custom CSS
              </label>
              <span className="text-[11px] text-gray-400">
                Footer-Selektoren werden strikt gefiltert
              </span>
            </div>
            <textarea
              {...register("customCss")}
              rows={4}
              placeholder="/* Eigene Styles */&#10;.custom-card { border-radius: 12px; }"
              className="w-full font-mono rounded-xl border border-gray-300 bg-gray-50 p-3 text-xs text-gray-900 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-slate-800/80 dark:text-white"
            />
          </div>

          <button
            type="submit"
            disabled={saving}
            className="inline-flex items-center justify-center gap-2 rounded-xl bg-brand-500 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-brand-600 disabled:opacity-50"
          >
            <FloppyDiscIcon className="size-4" />
            {saving ? "Wird gespeichert..." : "Änderungen speichern"}
          </button>
        </form>

        {/* Rechte Spalte: Live-Vorschau */}
        <div className="flex flex-col overflow-hidden rounded-2xl border border-gray-200 bg-gray-900 shadow-xl dark:border-white/10 dark:bg-slate-950">
          <div className="flex items-center justify-between border-b border-white/10 bg-slate-900/90 px-4 py-3">
            <span className="text-xs font-semibold tracking-wider text-gray-400 uppercase">
              Live-Vorschau
            </span>
            <div className="flex items-center gap-1.5">
              <div className="size-2.5 rounded-full bg-red-500/70" />
              <div className="size-2.5 rounded-full bg-amber-500/70" />
              <div className="size-2.5 rounded-full bg-emerald-500/70" />
            </div>
          </div>

          <div className="flex flex-1 flex-col justify-between p-6">
            <div className="flex flex-col gap-5">
              {/* Simulierte Headerbar */}
              <div className="flex items-center justify-between rounded-xl border border-white/10 bg-slate-800/50 p-3.5 backdrop-blur-sm">
                <div className="flex items-center gap-3">
                  {branding?.logoDarkUrl ? (
                    <img
                      src={branding.logoDarkUrl}
                      alt="Logo"
                      className="h-7 w-auto max-w-[120px] object-contain"
                    />
                  ) : (
                    <div
                      style={{ backgroundColor: liveValues.primaryColor }}
                      className="size-7 rounded-lg flex items-center justify-center font-bold text-white text-xs"
                    >
                      M
                    </div>
                  )}
                  <span className="font-bold text-white text-base">
                    {liveValues.panelName || "Midnight Panel"}
                  </span>
                </div>
                <button
                  type="button"
                  style={{ backgroundColor: liveValues.primaryColor }}
                  className="rounded-lg px-3.5 py-1.5 text-xs font-medium text-white shadow-sm transition"
                >
                  Primär-Button
                </button>
              </div>

              {/* Beispiel-Inhalt */}
              <div className="rounded-xl border border-white/10 bg-slate-800/30 p-5">
                <div className="flex items-center justify-between">
                  <h4 className="text-sm font-semibold text-gray-200">Server Status</h4>
                  <span
                    style={{ backgroundColor: liveValues.accentColor }}
                    className="rounded-md px-2 py-0.5 text-[10px] font-bold text-white"
                  >
                    Akzent-Farbe
                  </span>
                </div>
                <p className="mt-2 text-xs text-gray-400">
                  So wirken die eingestellten Primär- und Akzentfarben im Panel-Design.
                </p>
                <div className="mt-4 flex gap-2">
                  <div
                    style={{ borderColor: liveValues.primaryColor }}
                    className="flex-1 rounded-lg border bg-slate-800/50 p-3 text-center"
                  >
                    <span className="block text-lg font-bold text-white">4</span>
                    <span className="text-[11px] text-gray-400">Nodes</span>
                  </div>
                  <div className="flex-1 rounded-lg border border-white/5 bg-slate-800/50 p-3 text-center">
                    <span className="block text-lg font-bold text-white">18</span>
                    <span className="text-[11px] text-gray-400">Shards</span>
                  </div>
                </div>
              </div>
            </div>

            {/* Unveränderbare Footer-Vorschau */}
            <div className="mt-8 overflow-hidden rounded-xl border border-white/10">
              <PoweredByFooter />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
