import { extensionsApi } from "@/api/extensions";
import type { ExtensionSummary, PagedQuery } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { RequirePermission } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import {
  BarChartIcon,
  BoltIcon,
  ChatIcon,
  ChevronRightIcon,
  DatabaseIcon,
  DownloadIcon,
  FileIcon,
  FilterIcon,
  GearIcon,
  GlobeIcon,
  LinkIcon,
  LockIcon,
  PluginIcon,
  PlusIcon,
  SearchIcon,
  TrashBinIcon,
} from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Link } from "react-router";
import { ConfigModal } from "./config-modal";
import { InstallModal } from "./install-modal";

function renderExtensionIcon(icon?: string | null, className = "size-5") {
  switch (icon?.toLowerCase()) {
    case "chat":
    case "discord":
      return <ChatIcon className={className} />;
    case "database":
    case "backup":
      return <DatabaseIcon className={className} />;
    case "shield":
    case "lock":
    case "security":
      return <LockIcon className={className} />;
    case "chart":
    case "analytics":
      return <BarChartIcon className={className} />;
    case "globe":
    case "cloudflare":
    case "dns":
      return <GlobeIcon className={className} />;
    case "bolt":
      return <BoltIcon className={className} />;
    default:
      return <PluginIcon className={className} />;
  }
}

export default function ExtensionsPage() {
  const { t } = useT();
  const { can } = useAuth();
  const queryClient = useQueryClient();

  const [search, setSearch] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<string>("");
  const [activeFilter, setActiveFilter] = useState<"all" | "active" | "inactive">("all");
  const [page, setPage] = useState(1);
  const pageSize = 24;

  const [installModalOpen, setInstallModalOpen] = useState(false);
  const [configModalExtension, setConfigModalExtension] = useState<ExtensionSummary | null>(null);
  const [togglingId, setTogglingId] = useState<number | null>(null);

  // Fetch categories
  const { data: categories = [] } = useQuery({
    queryKey: ["admin", "extensions", "categories"],
    queryFn: () => extensionsApi.getCategories(),
  });

  // Query extensions
  const query: PagedQuery = {
    page,
    pageSize,
    search: search.trim() || undefined,
  };

  const { data: pagedExtensions, isLoading } = useQuery({
    queryKey: ["admin", "extensions", "list", query, selectedCategory],
    queryFn: () => extensionsApi.list(query, selectedCategory || undefined),
  });

  const filteredItems = useMemo(() => {
    if (!pagedExtensions?.items) return [];
    if (activeFilter === "active") {
      return pagedExtensions.items.filter((x) => x.isEnabled);
    }
    if (activeFilter === "inactive") {
      return pagedExtensions.items.filter((x) => !x.isEnabled);
    }
    return pagedExtensions.items;
  }, [pagedExtensions?.items, activeFilter]);

  const activeCount = useMemo(
    () => pagedExtensions?.items.filter((x) => x.isEnabled).length || 0,
    [pagedExtensions?.items],
  );

  const handleToggle = async (ext: ExtensionSummary, newStatus: boolean) => {
    setTogglingId(ext.id);
    try {
      await extensionsApi.toggle(ext.id, newStatus);
      await queryClient.invalidateQueries({ queryKey: ["admin", "extensions"] });
      notifySuccess(
        t(
          "Extensions.Toggled",
          ext.name,
          newStatus ? t("Extensions.Enabled") : t("Extensions.Disabled"),
        ),
      );
    } catch (err) {
      notifyError(err);
    } finally {
      setTogglingId(null);
    }
  };

  const handleDownload = async (ext: ExtensionSummary) => {
    try {
      await extensionsApi.downloadPackage(
        ext.id,
        ext.packageFileName || `${ext.identifier}-v${ext.version}.zip`,
      );
      notifySuccess("Paketdatei wird heruntergeladen…");
    } catch (err) {
      notifyError(err);
    }
  };

  const handleDelete = async (ext: ExtensionSummary) => {
    if (!confirm(t("Extensions.UninstallConfirm", ext.name))) return;
    try {
      await extensionsApi.delete(ext.id);
      await queryClient.invalidateQueries({ queryKey: ["admin", "extensions"] });
      notifySuccess(`Erweiterung "${ext.name}" deinstalliert.`);
    } catch (err) {
      notifyError(err);
    }
  };

  return (
    <RequirePermission permission={Permissions.AdminExtensionsRead}>
      <PageHeader
        title={t("Extensions.Title")}
        description={t("Extensions.Description")}
      />

      {/* Metrics Header */}
      <div className="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div className="rounded-2xl border border-gray-200 bg-white p-4.5 shadow-theme-xs dark:border-gray-800 dark:bg-white/3">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-gray-500 dark:text-gray-400">
              Installierte Module
            </span>
            <div className="flex size-7 items-center justify-center rounded-lg bg-brand-500/10 text-brand-400">
              <PluginIcon className="size-4" />
            </div>
          </div>
          <div className="mt-2 text-2xl font-bold text-gray-800 dark:text-white/90">
            {pagedExtensions?.totalCount ?? 0}
          </div>
        </div>

        <div className="rounded-2xl border border-gray-200 bg-white p-4.5 shadow-theme-xs dark:border-gray-800 dark:bg-white/3">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-gray-500 dark:text-gray-400">
              Aktivierte Module
            </span>
            <div className="flex size-7 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-400">
              <BoltIcon className="size-4" />
            </div>
          </div>
          <div className="mt-2 text-2xl font-bold text-emerald-500 dark:text-emerald-400">
            {activeCount}
          </div>
        </div>

        <div className="rounded-2xl border border-gray-200 bg-white p-4.5 shadow-theme-xs dark:border-gray-800 dark:bg-white/3">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-gray-500 dark:text-gray-400">
              Kategorien
            </span>
            <div className="flex size-7 items-center justify-center rounded-lg bg-indigo-500/10 text-indigo-400">
              <FilterIcon className="size-4" />
            </div>
          </div>
          <div className="mt-2 text-2xl font-bold text-gray-800 dark:text-white/90">
            {categories.length}
          </div>
        </div>
      </div>

      {/* Toolbar / Actions */}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-1 flex-wrap items-center gap-3">
          <div className="relative min-w-[220px] flex-1 sm:max-w-xs">
            <SearchIcon className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-gray-400" />
            <Input
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              placeholder={t("Extensions.SearchPlaceholder")}
              className="pl-9"
            />
          </div>

          {/* Category Dropdown */}
          <div className="flex items-center gap-2">
            <FilterIcon className="size-4 text-gray-400" />
            <select
              value={selectedCategory}
              onChange={(e) => {
                setSelectedCategory(e.target.value);
                setPage(1);
              }}
              className="flex h-11 min-w-[160px] rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
            >
              <option value="">{t("Extensions.AllCategories")}</option>
              {categories.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </div>

          {/* Status Tabs */}
          <div className="flex items-center rounded-lg border border-gray-200 p-0.5 text-xs dark:border-gray-800">
            <button
              type="button"
              onClick={() => setActiveFilter("all")}
              className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                activeFilter === "all"
                  ? "bg-brand-500 text-white"
                  : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
              }`}
            >
              Alle
            </button>
            <button
              type="button"
              onClick={() => setActiveFilter("active")}
              className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                activeFilter === "active"
                  ? "bg-brand-500 text-white"
                  : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
              }`}
            >
              Aktiv
            </button>
            <button
              type="button"
              onClick={() => setActiveFilter("inactive")}
              className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                activeFilter === "inactive"
                  ? "bg-brand-500 text-white"
                  : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
              }`}
            >
              Inaktiv
            </button>
          </div>
        </div>

        {can(Permissions.AdminExtensionsWrite) && (
          <div className="flex items-center gap-2">
            <Button
              onClick={() => setInstallModalOpen(true)}
              className="w-full sm:w-auto"
            >
              <PlusIcon className="mr-1.5 size-4" />
              Erweiterung installieren (Datei / Link)
            </Button>
          </div>
        )}
      </div>

      {/* Grid of Extension Cards */}
      {isLoading ? (
        <div className="flex h-64 items-center justify-center text-sm text-gray-500 dark:text-gray-400">
          Lade Erweiterungen…
        </div>
      ) : filteredItems.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-gray-200 bg-gray-50/50 p-12 text-center dark:border-gray-800 dark:bg-white/3">
          <PluginIcon className="mx-auto mb-3 size-12 text-gray-400 opacity-60" />
          <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">
            {t("Extensions.NoExtensions")}
          </h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Installiere eine Erweiterung per Datei-Upload oder über einen Download-Link.
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filteredItems.map((ext) => (
            <div
              key={ext.id}
              className="group relative flex flex-col justify-between rounded-2xl border border-gray-200 bg-white p-5 shadow-theme-xs transition-all duration-200 hover:border-brand-300 hover:shadow-md dark:border-gray-800 dark:bg-white/3 dark:hover:border-brand-500/40"
            >
              <div>
                {/* Header with Icon, Name, Category and Toggle */}
                <div className="flex items-start justify-between gap-3">
                  <div className="flex items-center gap-3">
                    <div
                      className={`flex size-11 items-center justify-center rounded-xl border transition-all ${
                        ext.isEnabled
                          ? "border-brand-200 bg-brand-50 text-brand-500 dark:border-brand-500/30 dark:bg-brand-500/10 dark:text-brand-400"
                          : "border-gray-200 bg-gray-100 text-gray-400 dark:border-gray-800 dark:bg-gray-800 dark:text-gray-500"
                      }`}
                    >
                      {renderExtensionIcon(ext.icon, "size-6")}
                    </div>
                    <div>
                      <Link
                        to={`/admin/extensions/${ext.id}`}
                        className="line-clamp-1 font-semibold text-gray-800 transition-colors hover:text-brand-500 dark:text-white/90 dark:hover:text-brand-400"
                      >
                        {ext.name}
                      </Link>
                      <div className="mt-0.5 flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-400">
                        <span className="font-medium text-brand-500 dark:text-brand-400">
                          {ext.category}
                        </span>
                        <span>•</span>
                        <span>v{ext.version}</span>
                      </div>
                    </div>
                  </div>

                  {can(Permissions.AdminExtensionsWrite) && (
                    <div className="flex items-center gap-2">
                      <Switch
                        size="sm"
                        checked={ext.isEnabled}
                        disabled={togglingId === ext.id}
                        onCheckedChange={(checked) => handleToggle(ext, checked)}
                      />
                    </div>
                  )}
                </div>

                {/* Description */}
                <p className="mt-3.5 min-h-[36px] text-xs text-gray-600 line-clamp-2 dark:text-gray-300">
                  {ext.description || "Keine Beschreibung verfügbar."}
                </p>

                {/* Metadata & Source Tags */}
                <div className="mt-3 flex flex-wrap items-center gap-2 text-[11px] text-gray-500 dark:text-gray-400">
                  <span className="rounded-md bg-gray-100 px-2 py-0.5 dark:bg-white/5">
                    Von {ext.author}
                  </span>

                  {ext.installSource === "url" ? (
                    <span className="flex items-center gap-1 rounded-md bg-indigo-500/10 px-2 py-0.5 text-indigo-400">
                      <LinkIcon className="size-3" /> Link
                    </span>
                  ) : ext.installSource === "file" ? (
                    <span className="flex items-center gap-1 rounded-md bg-brand-500/10 px-2 py-0.5 text-brand-400">
                      <FileIcon className="size-3" /> Datei
                    </span>
                  ) : (
                    <span className="flex items-center gap-1 rounded-md bg-gray-100 px-2 py-0.5 text-gray-500 dark:bg-white/5 dark:text-gray-400">
                      <BoltIcon className="size-3" /> System
                    </span>
                  )}

                  <Badge
                    variant="light"
                    color={ext.isEnabled ? "success" : "warning"}
                    size="sm"
                  >
                    {ext.isEnabled ? t("Extensions.Enabled") : t("Extensions.Disabled")}
                  </Badge>
                </div>
              </div>

              {/* Bottom Actions */}
              <div className="mt-5 flex items-center justify-between border-t border-gray-100 pt-3 dark:border-white/5">
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setConfigModalExtension(ext)}
                  className="gap-1.5 text-xs text-gray-600 dark:text-gray-400"
                >
                  <GearIcon className="size-3.5" />
                  {t("Extensions.Configure")}
                </Button>

                <div className="flex items-center gap-1">
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    onClick={() => handleDownload(ext)}
                    className="text-gray-400 hover:text-brand-500 dark:hover:text-brand-400"
                    title="Paketdatei herunterladen"
                  >
                    <DownloadIcon className="size-4" />
                  </Button>

                  {can(Permissions.AdminExtensionsWrite) && (
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      onClick={() => handleDelete(ext)}
                      className="text-gray-400 hover:text-red-500 dark:hover:text-red-400"
                      title={t("Extensions.Uninstall")}
                    >
                      <TrashBinIcon className="size-4" />
                    </Button>
                  )}

                  <Link to={`/admin/extensions/${ext.id}`}>
                    <Button variant="ghost" size="icon-sm" title="Details öffnen">
                      <ChevronRightIcon className="size-4 text-gray-400" />
                    </Button>
                  </Link>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Modals */}
      <InstallModal
        open={installModalOpen}
        onOpenChange={setInstallModalOpen}
      />

      <ConfigModal
        extension={configModalExtension}
        open={!!configModalExtension}
        onOpenChange={(open) => !open && setConfigModalExtension(null)}
      />
    </RequirePermission>
  );
}
