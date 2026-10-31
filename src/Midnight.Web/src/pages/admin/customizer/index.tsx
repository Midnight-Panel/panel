import { useState, useEffect } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { toast } from "sonner";
import { useBranding } from "@/contexts/BrandingContext";
import { brandingApi } from "@/api/branding";
import { THEME_PRESETS, type ThemePreset } from "./presets";
import { ColorPicker } from "./components/color-picker";
import { GradientEditor } from "./components/gradient-editor";
import { LogoUploadCard } from "./components/logo-upload-card";
import { LivePreview } from "./components/live-preview";
import { ResetModal, type ResetScope } from "./components/reset-modal";
import { CssEditor } from "./components/css-editor";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import {
  BrushIcon,
  FloppyDiscIcon,
  RegenerateIcon,
  SparkIcon,
  UploadIcon,
  LockIcon,
  CodeIcon,
  CheckIcon,
  LayoutIcon,
} from "@/icons";

const brandingSchema = z.object({
  panelName: z
    .string()
    .min(2, "Der Panel-Name muss mindestens 2 Zeichen lang sein")
    .max(64, "Der Panel-Name darf maximal 64 Zeichen lang sein"),
  primaryColor: z
    .string()
    .regex(/^#[0-9A-Fa-f]{6}$/, "Ungültiger Hex-Code (#RRGGBB)"),
  accentColor: z
    .string()
    .regex(/^#[0-9A-Fa-f]{6}$/, "Ungültiger Hex-Code (#RRGGBB)"),
  loginBackgroundType: z.enum(["none", "gradient", "image"]),
  loginBackgroundValue: z.string().optional().nullable(),
  customCss: z.string().optional().nullable(),
});

type FormValues = z.infer<typeof brandingSchema>;

type SectionTab = "general" | "colors" | "logos" | "login" | "css";

export default function AdminCustomizerPage() {
  const { branding, loading, reload } = useBranding();
  const [activeTab, setActiveTab] = useState<SectionTab>("general");
  const [mobileView, setMobileView] = useState<"settings" | "preview">("settings");
  const [resetModalOpen, setResetModalOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const {
    register,
    handleSubmit,
    control,
    watch,
    setValue,
    reset,
    formState: { isDirty, errors },
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

  // Sync form values when branding data is loaded or reloaded
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

  // Warn user before navigating away with unsaved changes
  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      if (isDirty) {
        e.preventDefault();
        e.returnValue = "";
      }
    };
    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [isDirty]);

  const liveValues = watch();

  const handleApplyPreset = (preset: ThemePreset) => {
    setValue("primaryColor", preset.primaryColor, { shouldDirty: true, shouldValidate: true });
    setValue("accentColor", preset.accentColor, { shouldDirty: true, shouldValidate: true });
    setValue("loginBackgroundType", "gradient", { shouldDirty: true, shouldValidate: true });
    setValue("loginBackgroundValue", preset.gradient, { shouldDirty: true, shouldValidate: true });
    toast.success(`Theme-Preset "${preset.name}" angewendet!`);
  };

  const onSubmit = async (values: FormValues) => {
    setSaving(true);
    try {
      await brandingApi.update({
        panelName: values.panelName.trim(),
        primaryColor: values.primaryColor,
        accentColor: values.accentColor,
        loginBackgroundType: values.loginBackgroundType,
        loginBackgroundValue: values.loginBackgroundValue ?? null,
        customCss: values.customCss ?? null,
      });
      toast.success("Branding-Einstellungen erfolgreich gespeichert!");
      await reload();
    } catch (err: any) {
      toast.error(
        err?.detail ||
          err?.message ||
          "Fehler beim Speichern der Branding-Einstellungen.",
      );
    } finally {
      setSaving(false);
    }
  };

  const handleResetScope = async (scope: ResetScope) => {
    if (scope === "all") {
      try {
        await brandingApi.reset();
        toast.success("Alle Branding-Einstellungen auf Standard zurückgesetzt!");
        await reload();
      } catch (err: any) {
        toast.error(err?.detail || err?.message || "Fehler beim Zurücksetzen.");
      }
      return;
    }

    if (scope === "colors") {
      setValue("primaryColor", "#465FFF", { shouldDirty: true });
      setValue("accentColor", "#7C3AED", { shouldDirty: true });
      setValue("loginBackgroundType", "gradient", { shouldDirty: true });
      setValue(
        "loginBackgroundValue",
        "linear-gradient(135deg, #0F172A 0%, #1E1B4B 50%, #0F172A 100%)",
        { shouldDirty: true },
      );
      toast.info("Farben und Verlauf zurückgesetzt. Speichere, um zu bestätigen.");
      return;
    }

    if (scope === "logos") {
      try {
        await brandingApi.deleteLogo("light");
        await brandingApi.deleteLogo("dark");
        await brandingApi.deleteFavicon();
        toast.success("Logos und Favicon auf Standard zurückgesetzt!");
        await reload();
      } catch (err: any) {
        toast.error(err?.detail || err?.message || "Fehler beim Entfernen der Grafiken.");
      }
      return;
    }

    if (scope === "css") {
      setValue("customCss", "", { shouldDirty: true });
      toast.info("Custom CSS geleert. Speichere, um die Änderung zu aktivieren.");
      return;
    }
  };

  if (loading && !branding) {
    return (
      <div className="p-6 max-w-7xl mx-auto space-y-6">
        <div className="flex items-center justify-between">
          <div className="space-y-2">
            <Skeleton className="h-8 w-64 rounded-lg" />
            <Skeleton className="h-4 w-96 rounded-lg" />
          </div>
          <Skeleton className="h-9 w-36 rounded-lg" />
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
          <div className="lg:col-span-7 space-y-4">
            <Skeleton className="h-12 w-full rounded-xl" />
            <Skeleton className="h-64 w-full rounded-2xl" />
          </div>
          <div className="lg:col-span-5">
            <Skeleton className="h-96 w-full rounded-2xl" />
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1520px] mx-auto space-y-6">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-2 border-b border-gray-200 dark:border-gray-800">
        <div>
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-brand-500/10 text-brand-500 dark:text-brand-400">
              <BrushIcon className="size-5" />
            </div>
            <h1 className="text-xl sm:text-2xl font-outfit font-bold text-gray-900 dark:text-white">
              Customizer & Branding
            </h1>
            <Badge variant="light" color="primary" size="sm">
              Live
            </Badge>
          </div>
          <p className="text-xs sm:text-sm text-gray-500 dark:text-gray-400 mt-1">
            Personalisiere das Erscheinungsbild, Farben, Logos und die Login-Oberfläche deines Panels.
          </p>
        </div>

        {/* Global Reset Button with Safe Confirmation Modal */}
        <div className="flex items-center gap-2.5">
          {/* Mobile switcher */}
          <div className="lg:hidden flex items-center rounded-lg bg-gray-100 dark:bg-gray-800 p-0.5 text-xs">
            <button
              type="button"
              onClick={() => setMobileView("settings")}
              className={`px-3 py-1.5 rounded-md font-medium transition ${
                mobileView === "settings"
                  ? "bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow-xs"
                  : "text-gray-500 dark:text-gray-400"
              }`}
            >
              Einstellungen
            </button>
            <button
              type="button"
              onClick={() => setMobileView("preview")}
              className={`px-3 py-1.5 rounded-md font-medium transition ${
                mobileView === "preview"
                  ? "bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow-xs"
                  : "text-gray-500 dark:text-gray-400"
              }`}
            >
              Vorschau
            </button>
          </div>

          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => setResetModalOpen(true)}
            className="text-xs text-gray-600 dark:text-gray-300 hover:text-red-500 dark:hover:text-red-400"
          >
            <RegenerateIcon className="size-3.5 mr-1.5" />
            Zurücksetzen...
          </Button>
        </div>
      </div>

      {/* Main Layout Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
        {/* Left Column: Segmented Settings (7 cols) */}
        <div
          className={`lg:col-span-7 space-y-6 ${
            mobileView === "preview" ? "hidden lg:block" : "block"
          }`}
        >
          {/* Section Navigation Tabs */}
          <div className="flex items-center gap-1.5 overflow-x-auto p-1 rounded-xl bg-gray-100 dark:bg-white/[0.03] border border-gray-200/80 dark:border-white/5 scrollbar-none text-xs">
            <button
              type="button"
              onClick={() => setActiveTab("general")}
              className={`flex items-center gap-2 px-3.5 py-2 rounded-lg font-medium whitespace-nowrap transition-all duration-150 ${
                activeTab === "general"
                  ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs font-semibold"
                  : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
              }`}
            >
              <LayoutIcon className="size-3.5" />
              <span>Allgemein</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab("colors")}
              className={`flex items-center gap-2 px-3.5 py-2 rounded-lg font-medium whitespace-nowrap transition-all duration-150 ${
                activeTab === "colors"
                  ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs font-semibold"
                  : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
              }`}
            >
              <SparkIcon className="size-3.5" />
              <span>Farben & Presets</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab("logos")}
              className={`flex items-center gap-2 px-3.5 py-2 rounded-lg font-medium whitespace-nowrap transition-all duration-150 ${
                activeTab === "logos"
                  ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs font-semibold"
                  : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
              }`}
            >
              <UploadIcon className="size-3.5" />
              <span>Logos & Favicon</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab("login")}
              className={`flex items-center gap-2 px-3.5 py-2 rounded-lg font-medium whitespace-nowrap transition-all duration-150 ${
                activeTab === "login"
                  ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs font-semibold"
                  : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
              }`}
            >
              <LockIcon className="size-3.5" />
              <span>Login-Seite</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab("css")}
              className={`flex items-center gap-2 px-3.5 py-2 rounded-lg font-medium whitespace-nowrap transition-all duration-150 ${
                activeTab === "css"
                  ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs font-semibold"
                  : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
              }`}
            >
              <CodeIcon className="size-3.5" />
              <span>Custom CSS</span>
            </button>
          </div>

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
            {/* TAB: ALLGEMEIN */}
            {activeTab === "general" && (
              <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-5">
                <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                  <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                    Allgemeine Panel-Identität
                  </h3>
                  <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    Basisdaten deines Game-Server-Panels
                  </p>
                </div>

                <div className="space-y-2">
                  <label
                    htmlFor="panelName"
                    className="block text-sm font-semibold text-gray-900 dark:text-gray-100"
                  >
                    Panel-Name
                  </label>
                  <input
                    id="panelName"
                    type="text"
                    {...register("panelName")}
                    placeholder="Midnight Panel"
                    className="w-full rounded-xl border border-gray-200 bg-gray-50/70 px-4 py-2.5 text-sm text-gray-900 transition focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/[0.02] dark:text-white dark:focus:bg-transparent"
                  />
                  {errors.panelName ? (
                    <p className="text-xs text-red-500">{errors.panelName.message}</p>
                  ) : (
                    <p className="text-xs text-gray-500 dark:text-gray-400">
                      Wird im Browser-Tab, in E-Mail-Benachrichtigungen und in der oberen Navigationsleiste angezeigt.
                    </p>
                  )}
                </div>
              </div>
            )}

            {/* TAB: FARBEN & PRESETS */}
            {activeTab === "colors" && (
              <div className="space-y-6">
                {/* Presets Gallery */}
                <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-4">
                  <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                    <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                      Themen-Presets
                    </h3>
                    <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                      Vordefinierte, kontrastgeprüfte Farbkombinationen mit einem Klick aktivieren
                    </p>
                  </div>

                  <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
                    {THEME_PRESETS.map((preset) => {
                      const isCurrent =
                        liveValues.primaryColor.toUpperCase() ===
                          preset.primaryColor.toUpperCase() &&
                        liveValues.accentColor.toUpperCase() ===
                          preset.accentColor.toUpperCase();

                      return (
                        <button
                          key={preset.id}
                          type="button"
                          onClick={() => handleApplyPreset(preset)}
                          className={`group relative text-left p-3 rounded-xl border transition-all duration-150 ${
                            isCurrent
                              ? "border-brand-500 bg-brand-500/5 ring-2 ring-brand-500/20"
                              : "border-gray-200 dark:border-white/10 hover:border-gray-300 dark:hover:border-white/20 bg-gray-50/50 dark:bg-white/[0.01]"
                          }`}
                        >
                          <div className="flex items-center gap-1.5 mb-2">
                            <span
                              className="size-4 rounded-full ring-1 ring-white/20 shadow-xs"
                              style={{ backgroundColor: preset.primaryColor }}
                            />
                            <span
                              className="size-4 rounded-full ring-1 ring-white/20 shadow-xs"
                              style={{ backgroundColor: preset.accentColor }}
                            />
                            {isCurrent && (
                              <CheckIcon className="size-3.5 text-brand-500 ml-auto" />
                            )}
                          </div>
                          <div className="font-outfit font-medium text-xs text-gray-900 dark:text-white truncate">
                            {preset.name}
                          </div>
                          <div className="text-[10px] text-gray-500 dark:text-gray-400 line-clamp-1 mt-0.5">
                            {preset.description}
                          </div>
                        </button>
                      );
                    })}
                  </div>
                </div>

                {/* Individual Colors Card */}
                <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-5">
                  <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                    <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                      Individuelle Farbwerte
                    </h3>
                    <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                      Primär- und Akzentfarbe für Schaltflächen, Badges und Interaktionselemente
                    </p>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
                    <Controller
                      name="primaryColor"
                      control={control}
                      render={({ field }) => (
                        <ColorPicker
                          label="Primärfarbe"
                          description="Hauptschaltflächen, aktive Menüpunkte und Links"
                          value={field.value}
                          onChange={(val) => field.onChange(val)}
                        />
                      )}
                    />

                    <Controller
                      name="accentColor"
                      control={control}
                      render={({ field }) => (
                        <ColorPicker
                          label="Akzentfarbe"
                          description="Status-Badges, Highlights und sekundäre Aktionen"
                          value={field.value}
                          onChange={(val) => field.onChange(val)}
                        />
                      )}
                    />
                  </div>
                </div>
              </div>
            )}

            {/* TAB: LOGOS & FAVICON */}
            {activeTab === "logos" && (
              <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-5">
                <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                  <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                    Logos & Favicon
                  </h3>
                  <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    Lade hochauflösende Logodateien für helle und dunkle Umgebungen hoch
                  </p>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                  <LogoUploadCard
                    type="light"
                    title="Helles Logo"
                    description="Wird auf hellen Hintergründen und in E-Mails gerendert"
                    currentUrl={branding?.logoLightUrl}
                    onSuccess={reload}
                  />

                  <LogoUploadCard
                    type="dark"
                    title="Dunkles Logo"
                    description="Wird im dunklen Dashboard-Thema und der Sidebar genutzt"
                    currentUrl={branding?.logoDarkUrl}
                    onSuccess={reload}
                  />

                  <div className="md:col-span-2">
                    <LogoUploadCard
                      type="favicon"
                      title="Favicon"
                      description="Symbol für Browser-Tabs und Lesezeichen (optimal 32x32 oder 64x64)"
                      currentUrl={branding?.faviconUrl}
                      onSuccess={reload}
                    />
                  </div>
                </div>
              </div>
            )}

            {/* TAB: LOGIN-SEITE */}
            {activeTab === "login" && (
              <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-6">
                <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                  <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                    Login-Seiten Hintergrund
                  </h3>
                  <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    Wähle zwischen einem fließenden Farbverlauf, einem Hintergrundbild oder neutralem Design
                  </p>
                </div>

                {/* Segmented type selector */}
                <div className="space-y-2">
                  <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    Hintergrund-Modus
                  </label>
                  <div className="grid grid-cols-3 gap-2 p-1 rounded-xl bg-gray-100 dark:bg-white/[0.03] border border-gray-200 dark:border-white/5">
                    <button
                      type="button"
                      onClick={() => setValue("loginBackgroundType", "gradient", { shouldDirty: true })}
                      className={`py-2 px-3 rounded-lg text-xs font-medium transition-all ${
                        liveValues.loginBackgroundType === "gradient"
                          ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs font-semibold"
                          : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
                      }`}
                    >
                      Verlauf
                    </button>
                    <button
                      type="button"
                      onClick={() => setValue("loginBackgroundType", "none", { shouldDirty: true })}
                      className={`py-2 px-3 rounded-lg text-xs font-medium transition-all ${
                        liveValues.loginBackgroundType === "none"
                          ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs font-semibold"
                          : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
                      }`}
                    >
                      Keiner
                    </button>
                    <button
                      type="button"
                      onClick={() => setValue("loginBackgroundType", "image", { shouldDirty: true })}
                      className={`py-2 px-3 rounded-lg text-xs font-medium transition-all ${
                        liveValues.loginBackgroundType === "image"
                          ? "bg-white dark:bg-white/10 text-gray-900 dark:text-white shadow-xs font-semibold"
                          : "text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white"
                      }`}
                    >
                      Bild
                    </button>
                  </div>
                </div>

                {/* Mode: Gradient */}
                {liveValues.loginBackgroundType === "gradient" && (
                  <Controller
                    name="loginBackgroundValue"
                    control={control}
                    render={({ field }) => (
                      <GradientEditor
                        value={
                          field.value ||
                          "linear-gradient(135deg, #0F172A 0%, #1E1B4B 50%, #0F172A 100%)"
                        }
                        onChange={(val) => field.onChange(val)}
                      />
                    )}
                  />
                )}

                {/* Mode: Image */}
                {liveValues.loginBackgroundType === "image" && (
                  <div className="space-y-3">
                    <label className="block text-xs font-medium text-gray-700 dark:text-gray-300">
                      Bild-URL
                    </label>
                    <Controller
                      name="loginBackgroundValue"
                      control={control}
                      render={({ field }) => (
                        <input
                          type="url"
                          value={field.value || ""}
                          onChange={(e) => field.onChange(e.target.value)}
                          placeholder="https://example.com/wallpaper.jpg"
                          className="w-full rounded-xl border border-gray-200 bg-gray-50 px-3.5 py-2.5 text-xs text-gray-900 focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/[0.02] dark:text-white"
                        />
                      )}
                    />
                    <p className="text-[11px] text-gray-500 dark:text-gray-400">
                      Gib eine HTTPS-Bildadresse ein. Das Bild wird mit einem dezenten dunklen Overlay versehen, um Lesbarkeit zu garantieren.
                    </p>
                  </div>
                )}

                {/* Mode: None */}
                {liveValues.loginBackgroundType === "none" && (
                  <div className="p-4 rounded-xl border border-dashed border-gray-200 dark:border-gray-800 text-center text-xs text-gray-500">
                    Die Login-Seite verwendet das standardmäßige minimalistische Midnight-Theme ohne Verläufe.
                  </div>
                )}
              </div>
            )}

            {/* TAB: CUSTOM CSS */}
            {activeTab === "css" && (
              <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-5">
                <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                  <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                    Erweitertes Custom CSS
                  </h3>
                  <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    Füge individuelle CSS-Regeln zur Anpassung von Schriftarten, Rändern oder Karten ein
                  </p>
                </div>

                <Controller
                  name="customCss"
                  control={control}
                  render={({ field }) => (
                    <CssEditor
                      value={field.value || ""}
                      onChange={(val) => field.onChange(val)}
                      disabled={saving}
                    />
                  )}
                />
              </div>
            )}
          </form>
        </div>

        {/* Right Column: Sticky Live Preview (5 cols) */}
        <div
          className={`lg:col-span-5 lg:sticky lg:top-24 space-y-4 ${
            mobileView === "settings" ? "hidden lg:block" : "block"
          }`}
        >
          <LivePreview
            panelName={liveValues.panelName || "Midnight Panel"}
            primaryColor={liveValues.primaryColor || "#465FFF"}
            accentColor={liveValues.accentColor || "#7C3AED"}
            loginBackgroundType={liveValues.loginBackgroundType}
            loginBackgroundValue={liveValues.loginBackgroundValue}
            logoLightUrl={branding?.logoLightUrl}
            logoDarkUrl={branding?.logoDarkUrl}
          />
        </div>
      </div>

      {/* STICKY BOTTOM ACTION BAR (Only visible when form has unsaved changes) */}
      {isDirty && (
        <div className="fixed bottom-6 inset-x-0 z-40 max-w-2xl mx-auto px-4 animate-in fade-in-0 slide-in-from-bottom-5 duration-200">
          <div className="flex items-center justify-between gap-4 p-3.5 rounded-2xl border border-brand-500/30 bg-gray-900/95 dark:bg-[#0B0F19]/95 text-white shadow-2xl backdrop-blur-md">
            <div className="flex items-center gap-2.5 pl-2">
              <span className="size-2 rounded-full bg-amber-400 animate-pulse" />
              <span className="text-xs sm:text-sm font-medium">
                Ungespeicherte Änderungen vorhanden
              </span>
            </div>

            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => {
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
                  toast.info("Änderungen verworfen.");
                }}
                disabled={saving}
                className="h-8 text-xs text-gray-300 hover:text-white hover:bg-white/10"
              >
                Verwerfen
              </Button>

              <Button
                type="button"
                size="sm"
                onClick={handleSubmit(onSubmit)}
                disabled={saving}
                className="h-8 text-xs font-semibold bg-brand-500 hover:bg-brand-600 text-white shadow-xs"
              >
                <FloppyDiscIcon className="size-3.5 mr-1" />
                {saving ? "Wird gespeichert..." : "Änderungen speichern"}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Granular Reset Modal */}
      <ResetModal
        open={resetModalOpen}
        onOpenChange={setResetModalOpen}
        onConfirm={handleResetScope}
      />
    </div>
  );
}
