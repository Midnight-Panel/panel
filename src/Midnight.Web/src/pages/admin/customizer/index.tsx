import { useState, useEffect } from "react";
import { useForm, Controller, useFieldArray } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { toast } from "sonner";
import { useBranding } from "@/contexts/BrandingContext";
import { brandingApi } from "@/api/branding";
import type { FooterLinkItem } from "@/api/types";
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
  SparkIcon,
  UploadIcon,
  LockIcon,
  CodeIcon,
  CheckIcon,
  LayoutIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  PlusIcon,
  TrashBinIcon,
  ArrowUpIcon,
  ArrowDownIcon,
  HorizontalDotsIcon,
  EyeIcon,
} from "@/icons";

const footerLinkSchema: z.ZodType<FooterLinkItem> = z.object({
  id: z.string(),
  label: z.string().min(1, "Bezeichnung darf nicht leer sein"),
  url: z.string().min(1, "URL darf nicht leer sein"),
  newTab: z.boolean(),
});

const brandingSchema = z.object({
  panelName: z
    .string()
    .min(2, "Der Panel-Name muss mindestens 2 Zeichen lang sein")
    .max(64, "Der Panel-Name darf maximal 64 Zeichen lang sein"),
  slogan: z.string().max(128, "Maximal 128 Zeichen").optional().nullable(),
  panelUrl: z.string().optional().nullable(),
  defaultLanguage: z.string(),
  timeZone: z.string(),
  footerLinks: z.array(footerLinkSchema),
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
  const [resetModalOpen, setResetModalOpen] = useState(false);
  const [resetScope, setResetScope] = useState<ResetScope>("all");
  const [menuOpen, setMenuOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [previewCollapsed, setPreviewCollapsed] = useState(false);

  // Neuer Link im Footer-Formular
  const [newLinkLabel, setNewLinkLabel] = useState("");
  const [newLinkUrl, setNewLinkUrl] = useState("");
  const [newLinkNewTab, setNewLinkNewTab] = useState(true);

  const {
    register,
    handleSubmit,
    control,
    watch,
    setValue,
    reset,
    formState: { isDirty, dirtyFields, errors },
  } = useForm<FormValues>({
    resolver: zodResolver(brandingSchema),
    defaultValues: {
      panelName: branding?.panelName || "Midnight Panel",
      slogan: branding?.slogan || "Game Server Management",
      panelUrl: branding?.panelUrl || "http://localhost:5220",
      defaultLanguage: branding?.defaultLanguage || "de",
      timeZone: branding?.timeZone || "Europe/Berlin",
      footerLinks: branding?.footerLinks || [
        { id: "api-ref", label: "API-Referenz", url: "/api/reference", newTab: true },
        { id: "github", label: "GitHub", url: "https://github.com/Midnight-Panel/panel", newTab: true },
      ],
      primaryColor: branding?.primaryColor || "#465FFF",
      accentColor: branding?.accentColor || "#7C3AED",
      loginBackgroundType: branding?.loginBackgroundType || "gradient",
      loginBackgroundValue:
        branding?.loginBackgroundValue ||
        "linear-gradient(135deg, #0F172A 0%, #1E1B4B 50%, #0F172A 100%)",
      customCss: branding?.customCss || "",
    },
  });

  const { fields: footerLinkFields, append, remove, move } = useFieldArray({
    control,
    name: "footerLinks",
  });

  // Synchronisiere Formularwerte, wenn Branding geladen oder neu geladen wird
  useEffect(() => {
    if (branding) {
      const defaultLinks = [
        { id: "api-ref", label: "API-Referenz", url: "/api/reference", newTab: true },
        { id: "github", label: "GitHub", url: "https://github.com/Midnight-Panel/panel", newTab: true },
      ];

      reset({
        panelName: branding.panelName,
        slogan: branding.slogan || "Game Server Management",
        panelUrl: branding.panelUrl || "http://localhost:5220",
        defaultLanguage: branding.defaultLanguage || "de",
        timeZone: branding.timeZone || "Europe/Berlin",
        footerLinks: branding.footerLinks && branding.footerLinks.length > 0 ? branding.footerLinks : defaultLinks,
        primaryColor: branding.primaryColor,
        accentColor: branding.accentColor,
        loginBackgroundType: branding.loginBackgroundType,
        loginBackgroundValue: branding.loginBackgroundValue,
        customCss: branding.customCss || "",
      });
    }
  }, [branding, reset]);

  // Schutz vor Datenverlust beim Verlassen der Seite
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

  // Prüfen, ob ein bestimmter Tab ungespeicherte Änderungen hat
  const isTabDirty = (tab: SectionTab): boolean => {
    switch (tab) {
      case "general":
        return !!(
          dirtyFields.panelName ||
          dirtyFields.slogan ||
          dirtyFields.panelUrl ||
          dirtyFields.defaultLanguage ||
          dirtyFields.timeZone ||
          dirtyFields.footerLinks
        );
      case "colors":
        return !!(dirtyFields.primaryColor || dirtyFields.accentColor);
      case "login":
        return !!(dirtyFields.loginBackgroundType || dirtyFields.loginBackgroundValue);
      case "css":
        return !!dirtyFields.customCss;
      default:
        return false;
    }
  };

  const handleApplyPreset = (preset: ThemePreset) => {
    setValue("primaryColor", preset.primaryColor, { shouldDirty: true, shouldValidate: true });
    setValue("accentColor", preset.accentColor, { shouldDirty: true, shouldValidate: true });
    setValue("loginBackgroundType", "gradient", { shouldDirty: true, shouldValidate: true });
    setValue("loginBackgroundValue", preset.gradient, { shouldDirty: true, shouldValidate: true });
    toast.success(`Theme-Preset "${preset.name}" angewendet!`);
  };

  const handleAddFooterLink = () => {
    if (!newLinkLabel.trim() || !newLinkUrl.trim()) {
      toast.error("Bitte Bezeichnung und URL eingeben.");
      return;
    }

    append({
      id: Math.random().toString(36).substring(2, 9),
      label: newLinkLabel.trim(),
      url: newLinkUrl.trim(),
      newTab: newLinkNewTab,
    });

    setNewLinkLabel("");
    setNewLinkUrl("");
    setNewLinkNewTab(true);
    toast.success("Link zur Fußzeile hinzugefügt.");
  };

  const onSubmit = async (values: FormValues) => {
    setSaving(true);
    try {
      await brandingApi.update({
        panelName: values.panelName.trim(),
        slogan: values.slogan?.trim() ?? null,
        panelUrl: values.panelUrl?.trim() ?? null,
        defaultLanguage: values.defaultLanguage,
        timeZone: values.timeZone,
        footerLinks: values.footerLinks,
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

  const handleOpenReset = (scope: ResetScope) => {
    setResetScope(scope);
    setMenuOpen(false);
    setResetModalOpen(true);
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
      <div className="p-4 sm:p-6 lg:p-8 max-w-7xl mx-auto space-y-6">
        <div className="flex items-center justify-between pb-3 border-b border-gray-200 dark:border-gray-800">
          <div className="space-y-2">
            <Skeleton className="h-8 w-64 rounded-lg" />
            <Skeleton className="h-4 w-96 rounded-lg" />
          </div>
          <Skeleton className="h-9 w-32 rounded-lg" />
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
          <div className="lg:col-span-7 space-y-4">
            <Skeleton className="h-10 w-full rounded-xl" />
            <Skeleton className="h-72 w-full rounded-2xl" />
          </div>
          <div className="lg:col-span-5">
            <Skeleton className="h-96 w-full rounded-2xl" />
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1540px] mx-auto space-y-6">
      {/* Seitenkopf */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-3 border-b border-gray-200 dark:border-gray-800">
        <div>
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-brand-500/10 text-brand-500 dark:text-brand-400">
              <BrushIcon className="size-5" />
            </div>
            <h1 className="text-xl sm:text-2xl font-outfit font-bold text-gray-900 dark:text-white">
              Customizer & Branding
            </h1>
            <Badge variant="light" color="primary" size="sm" className="hidden sm:inline-flex">
              <SparkIcon className="size-3 mr-1 text-brand-400" />
              Live-Vorschau aktiv
            </Badge>
          </div>
          <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
            Passe das visuelle Erscheinungsbild, Logos, Farben und Texte deines Panels an.
          </p>
        </div>

        {/* Überlaufmenü "Optionen" */}
        <div className="relative flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => setMenuOpen(!menuOpen)}
            className="text-xs text-gray-700 dark:text-gray-300 font-medium"
          >
            <HorizontalDotsIcon className="size-4 mr-1.5 text-gray-500" />
            Optionen
            <ChevronDownIcon className="size-3.5 ml-1 text-gray-400" />
          </Button>

          {menuOpen && (
            <div
              className="absolute right-0 top-10 z-50 w-56 rounded-xl border border-gray-200 bg-white p-1.5 shadow-theme-lg dark:border-gray-800 dark:bg-[#161619] text-xs space-y-1"
              onMouseLeave={() => setMenuOpen(false)}
            >
              <button
                type="button"
                onClick={() => handleOpenReset("colors")}
                className="w-full text-left px-3 py-2 rounded-lg hover:bg-gray-100 dark:hover:bg-white/5 text-gray-700 dark:text-gray-300 transition"
              >
                Farben & Presets zurücksetzen
              </button>
              <button
                type="button"
                onClick={() => handleOpenReset("logos")}
                className="w-full text-left px-3 py-2 rounded-lg hover:bg-gray-100 dark:hover:bg-white/5 text-gray-700 dark:text-gray-300 transition"
              >
                Logos & Favicon zurücksetzen
              </button>
              <button
                type="button"
                onClick={() => handleOpenReset("css")}
                className="w-full text-left px-3 py-2 rounded-lg hover:bg-gray-100 dark:hover:bg-white/5 text-gray-700 dark:text-gray-300 transition"
              >
                Custom CSS leeren
              </button>
              <div className="h-px bg-gray-200 dark:bg-gray-800 my-1" />
              <button
                type="button"
                onClick={() => handleOpenReset("all")}
                className="w-full text-left px-3 py-2 rounded-lg hover:bg-red-50 dark:hover:bg-red-950/30 text-red-600 dark:text-red-400 font-medium transition"
              >
                Alles auf Werkseinstellungen...
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Kollabierbare Vorschau unter 1100px */}
      <div className="min-[1101px]:hidden rounded-2xl border border-gray-200 dark:border-gray-800 bg-gray-50/50 dark:bg-white/[0.02] p-3.5">
        <button
          type="button"
          onClick={() => setPreviewCollapsed(!previewCollapsed)}
          className="w-full flex items-center justify-between text-xs font-semibold text-gray-800 dark:text-gray-200"
        >
          <span className="flex items-center gap-2">
            <EyeIcon className="size-4 text-brand-500" />
            <span>Live-Vorschau {previewCollapsed ? "einblenden" : "ausblenden"}</span>
          </span>
          {previewCollapsed ? <ChevronDownIcon className="size-4" /> : <ChevronUpIcon className="size-4" />}
        </button>

        {!previewCollapsed && (
          <div className="mt-3">
            <LivePreview
              panelName={liveValues.panelName || "Midnight Panel"}
              slogan={liveValues.slogan}
              primaryColor={liveValues.primaryColor || "#465FFF"}
              accentColor={liveValues.accentColor || "#7C3AED"}
              loginBackgroundType={liveValues.loginBackgroundType}
              loginBackgroundValue={liveValues.loginBackgroundValue}
              logoLightUrl={branding?.logoLightUrl}
              logoDarkUrl={branding?.logoDarkUrl}
              footerLinks={liveValues.footerLinks}
            />
          </div>
        )}
      </div>

      {/* Zweispaltiges Layout: Links Einstellungen (max ~720px), Rechts Sticky Live-Vorschau */}
      <div className="flex flex-col min-[1101px]:flex-row gap-8 items-start">
        {/* Linke Spalte (max ca. 720px) */}
        <div className="w-full min-[1101px]:max-w-[720px] flex-1 space-y-6">
          {/* Underline-Navigation mit animiertem Indikator & Dirty-Dot */}
          <div className="flex items-center gap-6 border-b border-gray-200 dark:border-gray-800 text-sm overflow-x-auto scrollbar-none">
            <button
              type="button"
              onClick={() => setActiveTab("general")}
              className={`relative py-3.5 font-medium transition-colors cursor-pointer select-none flex items-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 rounded-sm ${
                activeTab === "general"
                  ? "text-gray-900 dark:text-white font-semibold"
                  : "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
              }`}
            >
              <LayoutIcon className="size-4" />
              <span>Allgemein</span>
              {isTabDirty("general") && (
                <span className="size-1.5 rounded-full bg-amber-400 animate-pulse ml-0.5" />
              )}
              {activeTab === "general" && (
                <span className="absolute bottom-0 inset-x-0 h-0.5 bg-brand-500 rounded-full" />
              )}
            </button>

            <button
              type="button"
              onClick={() => setActiveTab("colors")}
              className={`relative py-3.5 font-medium transition-colors cursor-pointer select-none flex items-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 rounded-sm ${
                activeTab === "colors"
                  ? "text-gray-900 dark:text-white font-semibold"
                  : "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
              }`}
            >
              <SparkIcon className="size-4" />
              <span>Farben & Presets</span>
              {isTabDirty("colors") && (
                <span className="size-1.5 rounded-full bg-amber-400 animate-pulse ml-0.5" />
              )}
              {activeTab === "colors" && (
                <span className="absolute bottom-0 inset-x-0 h-0.5 bg-brand-500 rounded-full" />
              )}
            </button>

            <button
              type="button"
              onClick={() => setActiveTab("logos")}
              className={`relative py-3.5 font-medium transition-colors cursor-pointer select-none flex items-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 rounded-sm ${
                activeTab === "logos"
                  ? "text-gray-900 dark:text-white font-semibold"
                  : "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
              }`}
            >
              <UploadIcon className="size-4" />
              <span>Logos & Favicon</span>
              {activeTab === "logos" && (
                <span className="absolute bottom-0 inset-x-0 h-0.5 bg-brand-500 rounded-full" />
              )}
            </button>

            <button
              type="button"
              onClick={() => setActiveTab("login")}
              className={`relative py-3.5 font-medium transition-colors cursor-pointer select-none flex items-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 rounded-sm ${
                activeTab === "login"
                  ? "text-gray-900 dark:text-white font-semibold"
                  : "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
              }`}
            >
              <LockIcon className="size-4" />
              <span>Login-Seite</span>
              {isTabDirty("login") && (
                <span className="size-1.5 rounded-full bg-amber-400 animate-pulse ml-0.5" />
              )}
              {activeTab === "login" && (
                <span className="absolute bottom-0 inset-x-0 h-0.5 bg-brand-500 rounded-full" />
              )}
            </button>

            <button
              type="button"
              onClick={() => setActiveTab("css")}
              className={`relative py-3.5 font-medium transition-colors cursor-pointer select-none flex items-center gap-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 rounded-sm ${
                activeTab === "css"
                  ? "text-gray-900 dark:text-white font-semibold"
                  : "text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-200"
              }`}
            >
              <CodeIcon className="size-4" />
              <span>Custom CSS</span>
              {isTabDirty("css") && (
                <span className="size-1.5 rounded-full bg-amber-400 animate-pulse ml-0.5" />
              )}
              {activeTab === "css" && (
                <span className="absolute bottom-0 inset-x-0 h-0.5 bg-brand-500 rounded-full" />
              )}
            </button>
          </div>

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
            {/* TAB: ALLGEMEIN */}
            {activeTab === "general" && (
              <div className="space-y-6">
                {/* Sektion 1: Panel-Identität */}
                <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-5">
                  <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                    <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                      Panel-Identität
                    </h3>
                    <p className="text-[13px] text-gray-500 dark:text-gray-400 mt-0.5">
                      Name, Slogan und öffentliche Webadresse deines Panels
                    </p>
                  </div>

                  <div className="space-y-4">
                    <div className="space-y-1.5">
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
                        <p className="text-[13px] text-gray-500 dark:text-gray-400">
                          Wird im Browser-Tab, in der Navigation und in E-Mails verwendet.
                        </p>
                      )}
                    </div>

                    <div className="space-y-1.5">
                      <label
                        htmlFor="slogan"
                        className="block text-sm font-semibold text-gray-900 dark:text-gray-100"
                      >
                        Kurzbeschreibung / Slogan
                      </label>
                      <input
                        id="slogan"
                        type="text"
                        {...register("slogan")}
                        placeholder="Game Server Management"
                        className="w-full rounded-xl border border-gray-200 bg-gray-50/70 px-4 py-2.5 text-sm text-gray-900 transition focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/[0.02] dark:text-white dark:focus:bg-transparent"
                      />
                      <p className="text-[13px] text-gray-500 dark:text-gray-400">
                        Erscheint als Untertitel auf der Login-Seite und in System-E-Mails.
                      </p>
                    </div>

                    <div className="space-y-1.5">
                      <label
                        htmlFor="panelUrl"
                        className="block text-sm font-semibold text-gray-900 dark:text-gray-100"
                      >
                        Panel-URL
                      </label>
                      <input
                        id="panelUrl"
                        type="url"
                        {...register("panelUrl")}
                        placeholder="https://panel.example.com"
                        className="w-full rounded-xl border border-gray-200 bg-gray-50/70 px-4 py-2.5 text-sm text-gray-900 transition focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/[0.02] dark:text-white dark:focus:bg-transparent"
                      />
                      <p className="text-[13px] text-gray-500 dark:text-gray-400">
                        Öffentliche URL des Panels für Daemon-Verbindungen und Einladungslinks.
                      </p>
                    </div>
                  </div>
                </div>

                {/* Sektion 2: Sprache & Region */}
                <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-5">
                  <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                    <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                      Sprache & Region
                    </h3>
                    <p className="text-[13px] text-gray-500 dark:text-gray-400 mt-0.5">
                      Standardsprache für neue Besucher und regionale Zeitzone
                    </p>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                    <div className="space-y-1.5">
                      <label
                        htmlFor="defaultLanguage"
                        className="block text-sm font-semibold text-gray-900 dark:text-gray-100"
                      >
                        Standardsprache
                      </label>
                      <select
                        id="defaultLanguage"
                        {...register("defaultLanguage")}
                        className="w-full rounded-xl border border-gray-200 bg-gray-50/70 px-3.5 py-2.5 text-sm text-gray-900 transition focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-[#111827] dark:text-white"
                      >
                        <option value="de">Deutsch (de)</option>
                        <option value="en">English (en)</option>
                      </select>
                      <p className="text-[13px] text-gray-500 dark:text-gray-400">
                        Voreingestellte Sprache für neue Konten und Login.
                      </p>
                    </div>

                    <div className="space-y-1.5">
                      <label
                        htmlFor="timeZone"
                        className="block text-sm font-semibold text-gray-900 dark:text-gray-100"
                      >
                        Zeitzone
                      </label>
                      <select
                        id="timeZone"
                        {...register("timeZone")}
                        className="w-full rounded-xl border border-gray-200 bg-gray-50/70 px-3.5 py-2.5 text-sm text-gray-900 transition focus:border-brand-500 focus:bg-white focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-[#111827] dark:text-white"
                      >
                        <option value="Europe/Berlin">Europe/Berlin (UTC+1 / UTC+2)</option>
                        <option value="Europe/Vienna">Europe/Vienna (UTC+1 / UTC+2)</option>
                        <option value="Europe/Zurich">Europe/Zurich (UTC+1 / UTC+2)</option>
                        <option value="Europe/London">Europe/London (UTC+0 / UTC+1)</option>
                        <option value="America/New_York">America/New_York (UTC-5 / UTC-4)</option>
                        <option value="America/Los_Angeles">America/Los_Angeles (UTC-8 / UTC-7)</option>
                        <option value="UTC">UTC (Koordiniert)</option>
                      </select>
                      <p className="text-[13px] text-gray-500 dark:text-gray-400">
                        Genutzt für Backup-Zeitpläne und Log-Zeitstempel.
                      </p>
                    </div>
                  </div>
                </div>

                {/* Sektion 3: Footer & Externe Links */}
                <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-5">
                  <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                    <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                      Footer & Externe Links
                    </h3>
                    <p className="text-[13px] text-gray-500 dark:text-gray-400 mt-0.5">
                      Links im Dashboard-Footer (z.&nbsp;B. Discord, Dokumentation oder Status-Seite). System-Links erscheinen garantiert nur einmal.
                    </p>
                  </div>

                  {/* Liste vorhandener Links */}
                  <div className="space-y-2.5">
                    {footerLinkFields.map((field, idx) => (
                      <div
                        key={field.id}
                        className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-3.5 rounded-xl border border-gray-200/80 dark:border-white/5 bg-gray-50/50 dark:bg-white/[0.01]"
                      >
                        <div className="flex-1 grid grid-cols-1 sm:grid-cols-2 gap-3">
                          <div>
                            <span className="text-[11px] font-semibold text-gray-400 block mb-1">Name</span>
                            <input
                              type="text"
                              {...register(`footerLinks.${idx}.label`)}
                              className="w-full rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs text-gray-900 focus:border-brand-500 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                            />
                          </div>
                          <div>
                            <span className="text-[11px] font-semibold text-gray-400 block mb-1">URL</span>
                            <input
                              type="text"
                              {...register(`footerLinks.${idx}.url`)}
                              className="w-full rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs text-gray-900 focus:border-brand-500 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                            />
                          </div>
                        </div>

                        <div className="flex items-center gap-2 pt-1 sm:pt-4 self-end sm:self-auto">
                          <label className="flex items-center gap-1.5 text-[11px] text-gray-500 cursor-pointer mr-2">
                            <input
                              type="checkbox"
                              {...register(`footerLinks.${idx}.newTab`)}
                              className="rounded text-brand-500 focus:ring-brand-500"
                            />
                            <span>Neuer Tab</span>
                          </label>

                          <button
                            type="button"
                            onClick={() => idx > 0 && move(idx, idx - 1)}
                            disabled={idx === 0}
                            className="p-1.5 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 hover:text-white disabled:opacity-30"
                            title="Nach oben verschieben"
                          >
                            <ArrowUpIcon className="size-3.5" />
                          </button>

                          <button
                            type="button"
                            onClick={() => idx < footerLinkFields.length - 1 && move(idx, idx + 1)}
                            disabled={idx === footerLinkFields.length - 1}
                            className="p-1.5 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 hover:text-white disabled:opacity-30"
                            title="Nach unten verschieben"
                          >
                            <ArrowDownIcon className="size-3.5" />
                          </button>

                          <button
                            type="button"
                            onClick={() => remove(idx)}
                            className="p-1.5 rounded-lg border border-gray-200 dark:border-white/10 text-red-500 hover:bg-red-500/10"
                            title="Link entfernen"
                          >
                            <TrashBinIcon className="size-3.5" />
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>

                  {/* Neuen Link hinzufügen */}
                  <div className="p-4 rounded-xl border border-dashed border-gray-200 dark:border-gray-800 bg-gray-50/30 dark:bg-white/[0.01] space-y-3">
                    <span className="text-xs font-semibold text-gray-900 dark:text-white block">
                      + Weiteren Link hinzufügen
                    </span>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                      <input
                        type="text"
                        value={newLinkLabel}
                        onChange={(e) => setNewLinkLabel(e.target.value)}
                        placeholder="Link-Name (z. B. Discord)"
                        className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs text-gray-900 focus:border-brand-500 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                      />
                      <input
                        type="text"
                        value={newLinkUrl}
                        onChange={(e) => setNewLinkUrl(e.target.value)}
                        placeholder="https://..."
                        className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs text-gray-900 focus:border-brand-500 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                      />
                    </div>
                    <div className="flex items-center justify-between">
                      <label className="flex items-center gap-1.5 text-xs text-gray-500 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={newLinkNewTab}
                          onChange={(e) => setNewLinkNewTab(e.target.checked)}
                          className="rounded text-brand-500 focus:ring-brand-500"
                        />
                        <span>In neuem Tab öffnen</span>
                      </label>
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={handleAddFooterLink}
                        className="text-xs font-medium"
                      >
                        <PlusIcon className="size-3.5 mr-1" />
                        Hinzufügen
                      </Button>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* TAB: FARBEN & PRESETS */}
            {activeTab === "colors" && (
              <div className="space-y-6">
                <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-4">
                  <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                    <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                      Themen-Presets
                    </h3>
                    <p className="text-[13px] text-gray-500 dark:text-gray-400 mt-0.5">
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
                          className={`group relative text-left p-3.5 rounded-xl border transition-all duration-150 ${
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

                <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-white/[0.02] p-5 sm:p-6 space-y-5">
                  <div className="border-b border-gray-100 dark:border-gray-800/80 pb-3">
                    <h3 className="font-outfit font-semibold text-base text-gray-900 dark:text-white">
                      Individuelle Farbwerte
                    </h3>
                    <p className="text-[13px] text-gray-500 dark:text-gray-400 mt-0.5">
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
                  <p className="text-[13px] text-gray-500 dark:text-gray-400 mt-0.5">
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
                  <p className="text-[13px] text-gray-500 dark:text-gray-400 mt-0.5">
                    Wähle zwischen einem fließenden Farbverlauf, einem Hintergrundbild oder neutralem Design
                  </p>
                </div>

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
                    <p className="text-[13px] text-gray-500 dark:text-gray-400">
                      Gib eine HTTPS-Bildadresse ein. Das Bild wird mit einem dezenten dunklen Overlay versehen, um Lesbarkeit zu garantieren.
                    </p>
                  </div>
                )}

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
                  <p className="text-[13px] text-gray-500 dark:text-gray-400 mt-0.5">
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

        {/* Rechte Spalte: Sticky Live-Vorschau (ab 1101px) */}
        <div className="hidden min-[1101px]:block w-[420px] xl:w-[460px] shrink-0 sticky top-24 self-start space-y-4">
          <LivePreview
            panelName={liveValues.panelName || "Midnight Panel"}
            slogan={liveValues.slogan}
            primaryColor={liveValues.primaryColor || "#465FFF"}
            accentColor={liveValues.accentColor || "#7C3AED"}
            loginBackgroundType={liveValues.loginBackgroundType}
            loginBackgroundValue={liveValues.loginBackgroundValue}
            logoLightUrl={branding?.logoLightUrl}
            logoDarkUrl={branding?.logoDarkUrl}
            footerLinks={liveValues.footerLinks}
          />
        </div>
      </div>

      {/* STICKY BOTTOM ACTION BAR (Erscheint nur bei ungespeicherten Änderungen) */}
      {isDirty && (
        <div className="fixed bottom-6 inset-x-0 z-40 max-w-xl mx-auto px-4 animate-in fade-in-0 slide-in-from-bottom-5 duration-200">
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
                      slogan: branding.slogan || "Game Server Management",
                      panelUrl: branding.panelUrl || "http://localhost:5220",
                      defaultLanguage: branding.defaultLanguage || "de",
                      timeZone: branding.timeZone || "Europe/Berlin",
                      footerLinks: branding.footerLinks || [],
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

      {/* Granularer Reset-Modal */}
      <ResetModal
        open={resetModalOpen}
        onOpenChange={setResetModalOpen}
        onConfirm={handleResetScope}
        initialScope={resetScope}
      />
    </div>
  );
}
