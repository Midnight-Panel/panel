import { extensionsApi } from "@/api/extensions";
import type { ExtensionDetail } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { RequirePermission } from "@/components/shared/guards";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import {
  ArrowLeftIcon,
  BarChartIcon,
  BoltIcon,
  ChatIcon,
  DatabaseIcon,
  DocIcon,
  DownloadIcon,
  FileIcon,
  FloppyDiscIcon,
  FolderIcon,
  GearIcon,
  GlobeIcon,
  InfoCircleIcon,
  LinkIcon,
  LockIcon,
  PluginIcon,
  TrashBinIcon,
} from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";

function renderDetailIcon(icon?: string | null, className = "size-7") {
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

export default function ExtensionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const extensionId = Number(id);
  const { t } = useT();
  const { can } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [activeTab, setActiveTab] = useState("files");
  const [editorMode, setEditorMode] = useState<"visual" | "json">("visual");
  const [configJson, setConfigJson] = useState("{}");
  const [parsedValues, setParsedValues] = useState<Record<string, unknown>>({});
  const [jsonError, setJsonError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [toggling, setToggling] = useState(false);
  const [downloading, setDownloading] = useState(false);

  const { data: extension, isLoading } = useQuery<ExtensionDetail>({
    queryKey: ["admin", "extensions", extensionId],
    queryFn: () => extensionsApi.get(extensionId),
    enabled: !!extensionId,
  });

  useEffect(() => {
    if (extension?.configValuesJson) {
      try {
        const parsed = JSON.parse(extension.configValuesJson);
        setConfigJson(JSON.stringify(parsed, null, 2));
        setParsedValues(parsed);
        setJsonError(null);
      } catch {
        setConfigJson(extension.configValuesJson);
        setParsedValues({});
      }
    } else {
      setConfigJson("{}");
      setParsedValues({});
    }
  }, [extension]);

  const handleJsonChange = (val: string) => {
    setConfigJson(val);
    try {
      const parsed = JSON.parse(val);
      setParsedValues(parsed);
      setJsonError(null);
    } catch (e: unknown) {
      setJsonError(e instanceof Error ? e.message : "Invalid JSON format");
    }
  };

  const handleValueChange = (key: string, value: unknown) => {
    const updated = { ...parsedValues, [key]: value };
    setParsedValues(updated);
    setConfigJson(JSON.stringify(updated, null, 2));
    setJsonError(null);
  };

  const handleSaveConfig = async () => {
    try {
      const parsed = JSON.parse(configJson);
      setSaving(true);
      await extensionsApi.updateConfig(extensionId, JSON.stringify(parsed));
      await queryClient.invalidateQueries({ queryKey: ["admin", "extensions", extensionId] });
      notifySuccess(t("Extensions.SaveSuccess"));
    } catch (err: unknown) {
      if (err instanceof SyntaxError) {
        setJsonError("Invalid JSON format. Please check the syntax.");
      } else {
        notifyError(err);
      }
    } finally {
      setSaving(false);
    }
  };

  const handleToggle = async (checked: boolean) => {
    setToggling(true);
    try {
      await extensionsApi.toggle(extensionId, checked);
      await queryClient.invalidateQueries({ queryKey: ["admin", "extensions", extensionId] });
      notifySuccess(
        t(
          "Extensions.Toggled",
          extension?.name ?? "Extension",
          checked ? t("Extensions.Enabled") : t("Extensions.Disabled"),
        ),
      );
    } catch (err) {
      notifyError(err);
    } finally {
      setToggling(false);
    }
  };

  const handleDownload = async () => {
    if (!extension) return;
    setDownloading(true);
    try {
      await extensionsApi.downloadPackage(
        extension.id,
        extension.packageFileName || `${extension.identifier}-v${extension.version}.zip`,
      );
      notifySuccess("Package downloaded.");
    } catch (err) {
      notifyError(err);
    } finally {
      setDownloading(false);
    }
  };

  const handleDelete = async () => {
    if (!extension) return;
    if (!confirm(t("Extensions.UninstallConfirm", extension.name))) return;
    try {
      await extensionsApi.delete(extension.id);
      await queryClient.invalidateQueries({ queryKey: ["admin", "extensions"] });
      notifySuccess(`Extension "${extension.name}" uninstalled.`);
      navigate("/admin/extensions");
    } catch (err) {
      notifyError(err);
    }
  };

  const configKeys = useMemo(() => Object.keys(parsedValues), [parsedValues]);

  if (isLoading || !extension) {
    return (
      <div className="flex h-72 items-center justify-center text-sm text-gray-500 dark:text-gray-400">
        Loading extension…
      </div>
    );
  }

  return (
    <RequirePermission permission={Permissions.AdminExtensionsRead}>
      {/* Header & Back Link */}
      <div className="mb-6">
        <Link
          to="/admin/extensions"
          className="mb-4 inline-flex items-center gap-1.5 text-xs font-medium text-gray-500 transition-colors hover:text-brand-500 dark:text-gray-400 dark:hover:text-brand-400"
        >
          <ArrowLeftIcon className="size-3.5" />
          Back to extensions
        </Link>

        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <div
              className={`flex size-14 items-center justify-center rounded-2xl border transition-all ${
                extension.isEnabled
                  ? "border-brand-200 bg-brand-50 text-brand-500 dark:border-brand-500/30 dark:bg-brand-500/10 dark:text-brand-400"
                  : "border-gray-200 bg-gray-100 text-gray-400 dark:border-gray-800 dark:bg-gray-800 dark:text-gray-500"
              }`}
            >
              {renderDetailIcon(extension.icon, "size-8")}
            </div>

            <div>
              <div className="flex items-center gap-3">
                <h1 className="text-xl font-bold text-gray-900 dark:text-white/95">
                  {extension.name}
                </h1>
                <Badge
                  variant="light"
                  color={extension.isEnabled ? "success" : "warning"}
                  size="sm"
                >
                  {extension.isEnabled ? t("Extensions.Enabled") : t("Extensions.Disabled")}
                </Badge>
              </div>

              <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
                <span className="font-medium text-brand-500 dark:text-brand-400">
                  {extension.category}
                </span>
                <span>•</span>
                <span>v{extension.version}</span>
                <span>•</span>
                <span>By {extension.author}</span>
                <span>•</span>
                {extension.installSource === "url" ? (
                  <span className="inline-flex items-center gap-1 rounded bg-indigo-500/10 px-1.5 py-0.5 font-mono text-[11px] text-indigo-400">
                    <LinkIcon className="size-3" /> Link
                  </span>
                ) : extension.installSource === "file" ? (
                  <span className="inline-flex items-center gap-1 rounded bg-brand-500/10 px-1.5 py-0.5 font-mono text-[11px] text-brand-400">
                    <FileIcon className="size-3" /> File ({extension.packageFileName || "package"})
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-1 rounded bg-gray-100 px-1.5 py-0.5 font-mono text-[11px] text-gray-400 dark:bg-white/5">
                    <BoltIcon className="size-3" /> System
                  </span>
                )}
              </div>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <Button
              variant="outline"
              onClick={handleDownload}
              disabled={downloading}
              title="Download"
            >
              <DownloadIcon className="mr-1.5 size-4 text-brand-500" />
              Package (.zip)
            </Button>

            {can(Permissions.AdminExtensionsWrite) && (
              <>
                <div className="flex items-center gap-2.5 rounded-xl border border-gray-200 bg-white px-3 py-1.5 shadow-theme-xs dark:border-gray-800 dark:bg-white/3">
                  <span className="text-xs font-medium text-gray-700 dark:text-gray-300">
                    {extension.isEnabled ? "Enabled" : "Disabled"}
                  </span>
                  <Switch
                    checked={extension.isEnabled}
                    disabled={toggling}
                    onCheckedChange={handleToggle}
                  />
                </div>

                <Button
                  variant="outline"
                  onClick={handleDelete}
                  className="text-red-500 hover:border-red-500 hover:bg-red-50 hover:text-red-600 dark:border-red-500/30 dark:bg-red-500/5 dark:text-red-400 dark:hover:bg-red-500/10"
                >
                  <TrashBinIcon className="mr-1.5 size-4" />
                  {t("Extensions.Uninstall")}
                </Button>
              </>
            )}
          </div>
        </div>
      </div>

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
        <TabsList className="bg-gray-100 dark:bg-white/5">
          <TabsTrigger value="files" className="gap-2">
            <FolderIcon className="size-4" />
            Files ({extension.installedFiles.length})
          </TabsTrigger>
          <TabsTrigger value="config" className="gap-2">
            <GearIcon className="size-4" />
            {t("Extensions.Settings")}
          </TabsTrigger>
          <TabsTrigger value="readme" className="gap-2">
            <DocIcon className="size-4" />
            {t("Extensions.Readme")}
          </TabsTrigger>
          <TabsTrigger value="info" className="gap-2">
            <InfoCircleIcon className="size-4" />
            Information
          </TabsTrigger>
        </TabsList>

        {/* Tab 1: Installed Files */}
        <TabsContent value="files" className="space-y-4">
          <Card className="p-6">
            <div className="mb-4 flex items-center justify-between border-b border-gray-100 pb-4 dark:border-white/5">
              <div>
                <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">
                  Package files
                </h3>
                <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">
                  All files extracted for this extension in the panel storage.
                </p>
              </div>
              <Button variant="outline" size="sm" onClick={handleDownload} disabled={downloading}>
                <DownloadIcon className="mr-1.5 size-3.5" />
                Download as .zip
              </Button>
            </div>

            {extension.installedFiles.length === 0 ? (
              <div className="rounded-xl border border-dashed border-gray-200 p-8 text-center text-sm text-gray-500 dark:border-gray-800 dark:text-gray-400">
                No extracted files (built-in module).
              </div>
            ) : (
              <div className="max-h-96 space-y-1.5 overflow-y-auto font-mono text-xs">
                {extension.installedFiles.map((file, idx) => (
                  <div
                    key={idx}
                    className="flex items-center gap-2.5 rounded-lg border border-gray-100 bg-gray-50/50 px-3.5 py-2 transition-colors hover:bg-gray-100/60 dark:border-white/5 dark:bg-white/2 dark:hover:bg-white/4"
                  >
                    <FileIcon className="size-4 shrink-0 text-brand-500" />
                    <span className="text-gray-800 dark:text-gray-200">{file}</span>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </TabsContent>

        {/* Tab 2: Configuration */}
        <TabsContent value="config" className="space-y-4">
          <Card className="p-6">
            <div className="mb-4 flex items-center justify-between border-b border-gray-100 pb-4 dark:border-white/5">
              <div>
                <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">
                  Configuration
                </h3>
                <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">
                  Current settings for {extension.name}.
                </p>
              </div>

              <div className="flex items-center gap-2">
                <div className="flex items-center rounded-lg border border-gray-200 p-0.5 text-xs dark:border-gray-800">
                  <button
                    type="button"
                    onClick={() => setEditorMode("visual")}
                    className={`rounded-md px-3 py-1 font-medium transition-colors ${
                      editorMode === "visual"
                        ? "bg-brand-500 text-white"
                        : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
                    }`}
                  >
                    Form
                  </button>
                  <button
                    type="button"
                    onClick={() => setEditorMode("json")}
                    className={`rounded-md px-3 py-1 font-medium transition-colors ${
                      editorMode === "json"
                        ? "bg-brand-500 text-white"
                        : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
                    }`}
                  >
                    JSON
                  </button>
                </div>
              </div>
            </div>

            {editorMode === "visual" ? (
              <div className="space-y-4">
                {configKeys.length === 0 ? (
                  <div className="rounded-xl border border-dashed border-gray-200 p-10 text-center text-sm text-gray-500 dark:border-gray-800 dark:text-gray-400">
                    This extension has no configuration options.
                  </div>
                ) : (
                  configKeys.map((key) => {
                    const value = parsedValues[key];
                    const isBool = typeof value === "boolean";
                    const isNum = typeof value === "number";

                    if (isBool) {
                      return (
                        <div
                          key={key}
                          className="flex items-center justify-between rounded-xl border border-gray-100 bg-gray-50/50 p-4 dark:border-white/5 dark:bg-white/2"
                        >
                          <div>
                            <Label className="text-sm font-medium text-gray-800 dark:text-white/90">
                              {key}
                            </Label>
                            <span className="block text-xs text-gray-500 dark:text-gray-400">
                              Toggle {key}
                            </span>
                          </div>
                          <Switch
                            checked={Boolean(value)}
                            onCheckedChange={(checked) => handleValueChange(key, checked)}
                          />
                        </div>
                      );
                    }

                    return (
                      <div key={key} className="space-y-1.5">
                        <Label className="text-xs font-semibold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                          {key}
                        </Label>
                        <Input
                          type={isNum ? "number" : "text"}
                          value={value !== null && value !== undefined ? String(value) : ""}
                          onChange={(e) =>
                            handleValueChange(
                              key,
                              isNum ? Number(e.target.value) : e.target.value,
                            )
                          }
                          placeholder={`Value for ${key}`}
                          className="font-mono text-sm"
                        />
                      </div>
                    );
                  })
                )}
              </div>
            ) : (
              <div className="space-y-2">
                <Textarea
                  value={configJson}
                  onChange={(e) => handleJsonChange(e.target.value)}
                  className="h-80 font-mono text-xs leading-relaxed"
                  placeholder='{\n  "key": "value"\n}'
                  spellCheck={false}
                />
                {jsonError && (
                  <p className="text-xs font-medium text-red-500 dark:text-red-400">{jsonError}</p>
                )}
              </div>
            )}

            {can(Permissions.AdminExtensionsWrite) && configKeys.length > 0 && (
              <div className="mt-6 flex justify-end border-t border-gray-100 pt-4 dark:border-white/5">
                <Button onClick={handleSaveConfig} disabled={saving || !!jsonError}>
                  <FloppyDiscIcon className="mr-1.5 size-4" />
                  {saving ? "Saving…" : t("Common.Save")}
                </Button>
              </div>
            )}
          </Card>
        </TabsContent>

        {/* Tab 3: Readme / Documentation */}
        <TabsContent value="readme">
          <Card className="p-6">
            <h3 className="mb-4 text-base font-semibold text-gray-800 dark:text-white/90">
              {t("Extensions.Readme")}
            </h3>
            {extension.readmeMarkdown ? (
              <div className="prose prose-sm dark:prose-invert max-w-none rounded-xl border border-gray-100 bg-gray-50/50 p-6 font-mono text-xs leading-relaxed whitespace-pre-wrap dark:border-white/5 dark:bg-white/2">
                {extension.readmeMarkdown}
              </div>
            ) : (
              <p className="text-sm text-gray-500 dark:text-gray-400">
                No documentation provided for this extension.
              </p>
            )}
          </Card>
        </TabsContent>

        {/* Tab 4: Info & Manifest */}
        <TabsContent value="info">
          <Card className="p-6">
            <h3 className="mb-4 text-base font-semibold text-gray-800 dark:text-white/90">
              Module information
            </h3>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="rounded-xl border border-gray-100 p-4 dark:border-white/5">
                <span className="text-xs text-gray-500 dark:text-gray-400">Module identifier</span>
                <p className="mt-1 font-mono text-sm font-semibold text-gray-800 dark:text-white/90">
                  {extension.identifier}
                </p>
              </div>

              <div className="rounded-xl border border-gray-100 p-4 dark:border-white/5">
                <span className="text-xs text-gray-500 dark:text-gray-400">Install source</span>
                <p className="mt-1 text-sm font-semibold text-gray-800 dark:text-white/90">
                  {extension.installSource === "url"
                    ? "Download link (URL)"
                    : extension.installSource === "file"
                    ? "File upload"
                    : "Built-in system module"}
                </p>
              </div>

              {extension.sourceUrl && (
                <div className="col-span-full rounded-xl border border-gray-100 p-4 dark:border-white/5">
                  <span className="text-xs text-gray-500 dark:text-gray-400">Download link (URL)</span>
                  <p className="mt-1 break-all font-mono text-xs text-brand-500 dark:text-brand-400">
                    {extension.sourceUrl}
                  </p>
                </div>
              )}

              {extension.packageFileName && (
                <div className="rounded-xl border border-gray-100 p-4 dark:border-white/5">
                  <span className="text-xs text-gray-500 dark:text-gray-400">Package file name</span>
                  <p className="mt-1 font-mono text-sm font-semibold text-gray-800 dark:text-white/90">
                    {extension.packageFileName}
                  </p>
                </div>
              )}

              <div className="rounded-xl border border-gray-100 p-4 dark:border-white/5">
                <span className="text-xs text-gray-500 dark:text-gray-400">Version</span>
                <p className="mt-1 font-mono text-sm font-semibold text-gray-800 dark:text-white/90">
                  v{extension.version}
                </p>
              </div>

              <div className="rounded-xl border border-gray-100 p-4 dark:border-white/5">
                <span className="text-xs text-gray-500 dark:text-gray-400">Developer / author</span>
                <p className="mt-1 text-sm font-semibold text-gray-800 dark:text-white/90">
                  {extension.author}
                </p>
              </div>

              <div className="rounded-xl border border-gray-100 p-4 dark:border-white/5">
                <span className="text-xs text-gray-500 dark:text-gray-400">Category</span>
                <p className="mt-1 text-sm font-semibold text-gray-800 dark:text-white/90">
                  {extension.category}
                </p>
              </div>

              <div className="rounded-xl border border-gray-100 p-4 dark:border-white/5">
                <span className="text-xs text-gray-500 dark:text-gray-400">Installed at</span>
                <p className="mt-1 text-sm text-gray-800 dark:text-white/90">
                  {new Date(extension.installedAt).toLocaleString()}
                </p>
              </div>

              <div className="rounded-xl border border-gray-100 p-4 dark:border-white/5">
                <span className="text-xs text-gray-500 dark:text-gray-400">Last updated</span>
                <p className="mt-1 text-sm text-gray-800 dark:text-white/90">
                  {new Date(extension.updatedAt).toLocaleString()}
                </p>
              </div>
            </div>

            {extension.websiteUrl && (
              <div className="mt-4 rounded-xl border border-gray-100 p-4 dark:border-white/5">
                <span className="text-xs text-gray-500 dark:text-gray-400">Official website</span>
                <div className="mt-1">
                  <a
                    href={extension.websiteUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1.5 text-sm font-medium text-brand-500 hover:underline dark:text-brand-400"
                  >
                    <LinkIcon className="size-4" />
                    {extension.websiteUrl}
                  </a>
                </div>
              </div>
            )}
          </Card>
        </TabsContent>
      </Tabs>
    </RequirePermission>
  );
}
