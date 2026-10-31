import { shardsApi } from "@/api/shards";
import type {
  ShardConfigFile,
  ShardRequest,
  ShardVariable,
  ShardVariableRequest,
} from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { PageHeader } from "@/components/shared/page-header";
import NotFoundPage from "@/pages/not-found";
import { RequirePermission } from "@/components/shared/guards";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import {
  BoxIcon,
  CodeIcon,
  DownloadIcon,
  FloppyDiscIcon,
  GearIcon,
  PencilIcon,
  PlusIcon,
  TrashBinIcon,
} from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { useParams } from "react-router";
import { CodeEditor } from "./components/code-editor";

export default function ShardDetailPage() {
  const { id } = useParams<{ id: string }>();
  const shardId = Number(id);
  const { t } = useT();
  const { can } = useAuth();
  const queryClient = useQueryClient();

  const [activeTab, setActiveTab] = useState("general");
  const [busy, setBusy] = useState(false);

  // Variable Dialog State
  const [variableDialogOpen, setVariableDialogOpen] = useState(false);
  const [editingVariableId, setEditingVariableId] = useState<number | null>(null);

  // Config File Dialog State
  const [configFileDialogOpen, setConfigFileDialogOpen] = useState(false);
  const [editingConfigFileIndex, setEditingConfigFileIndex] = useState<number | null>(null);
  const [cfgPath, setCfgPath] = useState("");
  const [cfgParser, setCfgParser] = useState("properties");
  const [cfgMappingsJson, setCfgMappingsJson] = useState("{}");

  // Docker Image Dialog State
  const [imageDialogOpen, setImageDialogOpen] = useState(false);
  const [newImageLabel, setNewImageLabel] = useState("");
  const [newImageUri, setNewImageUri] = useState("");

  // Data Fetching
  const { data: shard, isLoading, isError } = useQuery({
    queryKey: ["admin", "shards", shardId],
    queryFn: () => shardsApi.getShard(shardId),
    enabled: !isNaN(shardId),
  });

  const { data: collections = [] } = useQuery({
    queryKey: ["admin", "shards", "collections", "all"],
    queryFn: () => shardsApi.listAllCollections(),
  });

  // Main Form
  const form = useForm<ShardRequest>({
    defaultValues: {
      collectionId: 1,
      name: "",
      author: "",
      version: "1.0.0",
      description: "",
      startupCommand: "",
      stopCommand: "",
      stopSignal: "SIGTERM",
      startupDone: "",
      defaultDockerImage: "",
      dockerImages: {},
      installScript: "",
      installImage: "ghcr.io/midnightpanel/installers:alpine",
      installInterpreter: "/bin/bash",
      configFiles: [],
      features: { eula: false, fileDenylist: [] },
    },
  });

  // Variable Form
  const variableForm = useForm<ShardVariableRequest>({
    defaultValues: {
      name: "",
      description: "",
      envVariable: "",
      defaultValue: "",
      userViewable: true,
      userEditable: true,
      rules: "required|string",
      sortOrder: 1,
    },
  });

  useEffect(() => {
    if (shard) {
      form.reset({
        collectionId: shard.collectionId,
        name: shard.name,
        author: shard.author,
        version: shard.version,
        description: shard.description || "",
        startupCommand: shard.startupCommand,
        stopCommand: shard.stopCommand || "",
        stopSignal: shard.stopSignal || "SIGTERM",
        startupDone: shard.startupDone || "",
        defaultDockerImage: shard.defaultDockerImage,
        dockerImages: shard.dockerImages || {},
        installScript: shard.installScript || "",
        installImage: shard.installImage,
        installInterpreter: shard.installInterpreter,
        configFiles: shard.configFiles || [],
        features: shard.features || { eula: false, fileDenylist: [] },
      });
    }
  }, [shard, form]);

  if (isNaN(shardId) || isError) return <NotFoundPage />;

  if (isLoading || !shard) {
    return (
      <div className="flex h-72 items-center justify-center text-gray-500 dark:text-gray-400">
        Loading shard details...
      </div>
    );
  }

  // Live Placeholders Extraction
  const watchedStartup = form.watch("startupCommand") || "";
  const placeholderMatches = Array.from(watchedStartup.matchAll(/\{\{([a-zA-Z0-9_-]+)\}\}/g)).map(
    (m) => m[1]
  );
  const uniquePlaceholders = Array.from(new Set(placeholderMatches));
  const definedEnvVars = new Set((shard.variables || []).map((v) => v.envVariable.toUpperCase()));
  const systemEnvVars = new Set(["SERVER_MEMORY", "SERVER_PORT", "SERVER_IP", "STARTUP"]);

  // Save Main Shard Settings
  const handleSaveMain = async (values: ShardRequest) => {
    setBusy(true);
    try {
      await shardsApi.updateShard(shardId, {
        ...values,
        collectionId: Number(values.collectionId),
      });
      await queryClient.invalidateQueries({ queryKey: ["admin", "shards", shardId] });
      notifySuccess(t("Shards.Saved"));
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  // Variable Operations
  const handleOpenAddVariable = () => {
    setEditingVariableId(null);
    variableForm.reset({
      name: "",
      description: "",
      envVariable: "",
      defaultValue: "",
      userViewable: true,
      userEditable: true,
      rules: "required|string",
      sortOrder: (shard.variables?.length || 0) + 1,
    });
    setVariableDialogOpen(true);
  };

  const handleOpenEditVariable = (v: ShardVariable) => {
    setEditingVariableId(v.id);
    variableForm.reset({
      name: v.name,
      description: v.description || "",
      envVariable: v.envVariable,
      defaultValue: v.defaultValue || "",
      userViewable: v.userViewable,
      userEditable: v.userEditable,
      rules: v.rules,
      sortOrder: v.sortOrder,
    });
    setVariableDialogOpen(true);
  };

  const handleSaveVariable = async (values: ShardVariableRequest) => {
    setBusy(true);
    try {
      if (editingVariableId) {
        await shardsApi.updateVariable(shardId, editingVariableId, values);
      } else {
        await shardsApi.createVariable(shardId, values);
      }
      await queryClient.invalidateQueries({ queryKey: ["admin", "shards", shardId] });
      setVariableDialogOpen(false);
      notifySuccess(t("Common.Saved"));
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  const handleDeleteVariable = async (v: ShardVariable) => {
    if (!confirm(t("Shards.VariablesTab.DeleteConfirm", v.name))) return;
    try {
      await shardsApi.deleteVariable(shardId, v.id);
      await queryClient.invalidateQueries({ queryKey: ["admin", "shards", shardId] });
      notifySuccess(t("Common.Saved"));
    } catch (err) {
      notifyError(err);
    }
  };

  // Config File Operations
  const handleOpenAddConfigFile = () => {
    setEditingConfigFileIndex(null);
    setCfgPath("");
    setCfgParser("properties");
    setCfgMappingsJson("{}");
    setConfigFileDialogOpen(true);
  };

  const handleOpenEditConfigFile = (cfg: ShardConfigFile, index: number) => {
    setEditingConfigFileIndex(index);
    setCfgPath(cfg.path);
    setCfgParser(cfg.parser);
    setCfgMappingsJson(JSON.stringify(cfg.mappings || {}, null, 2));
    setConfigFileDialogOpen(true);
  };

  const handleSaveConfigFile = () => {
    let mappings: Record<string, string>;
    try {
      mappings = JSON.parse(cfgMappingsJson);
      if (!mappings || typeof mappings !== "object" || Array.isArray(mappings)) throw new Error();
    } catch {
      notifyError("Invalid JSON in mappings.");
      return;
    }

    const currentConfigs = [...form.getValues("configFiles")];
    const newEntry: ShardConfigFile = {
      path: cfgPath.trim(),
      parser: cfgParser,
      mappings,
    };

    if (editingConfigFileIndex !== null) {
      currentConfigs[editingConfigFileIndex] = newEntry;
    } else {
      currentConfigs.push(newEntry);
    }

    form.setValue("configFiles", currentConfigs, { shouldDirty: true });
    setConfigFileDialogOpen(false);
  };

  const handleDeleteConfigFile = (index: number) => {
    const current = [...form.getValues("configFiles")];
    current.splice(index, 1);
    form.setValue("configFiles", current, { shouldDirty: true });
  };

  // Docker Images Operations
  const handleAddDockerImage = () => {
    if (!newImageLabel.trim() || !newImageUri.trim()) return;

    const current = { ...(form.getValues("dockerImages") || {}) };
    current[newImageLabel.trim()] = newImageUri.trim();

    form.setValue("dockerImages", current, { shouldDirty: true });

    if (!form.getValues("defaultDockerImage")) {
      form.setValue("defaultDockerImage", newImageUri.trim(), { shouldDirty: true });
    }

    setNewImageLabel("");
    setNewImageUri("");
    setImageDialogOpen(false);
  };

  const handleDeleteDockerImage = (label: string) => {
    const current = { ...(form.getValues("dockerImages") || {}) };
    const removedUri = current[label];
    delete current[label];
    form.setValue("dockerImages", current, { shouldDirty: true });

    // The default is stored as image uri, not as label
    if (form.getValues("defaultDockerImage") === removedUri) {
      form.setValue("defaultDockerImage", Object.values(current)[0] || "", { shouldDirty: true });
    }
  };

  return (
    <RequirePermission permission={Permissions.AdminShardsRead}>
      <PageHeader
        icon={<BoxIcon />}
        title={shard.name}
        crumbs={[{ label: t("Shards.Title"), to: "/admin/shards" }]}
        description={
          <span className="flex flex-wrap items-center gap-2">
            <Badge variant="light" color="primary" size="sm">{shard.collectionName}</Badge>
            <span>{shard.author} · v{shard.version}</span>
            <span className="text-xs text-gray-400 dark:text-gray-500">UUID: {shard.uuid}</span>
          </span>
        }
        actions={
          <>
            <Button
              variant="outline"
              onClick={() => shardsApi.exportShard(shard.id, shard.name)}
              className="w-full sm:w-auto"
            >
              <DownloadIcon className="size-4 mr-1.5 text-brand-500" />
              {t("Shards.Export")}
            </Button>

            {can(Permissions.AdminShardsWrite) && (
              <Button
                onClick={form.handleSubmit(handleSaveMain)}
                disabled={busy}
                className="w-full sm:w-auto"
              >
                <FloppyDiscIcon className="size-4 mr-1.5" />
                {busy ? "Saving..." : t("Common.Save")}
              </Button>
            )}
          </>
        }
      />

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
        <div>
          <TabsList variant="line">
            <TabsTrigger value="general" className="gap-2">
              <BoxIcon className="size-4" />
              {t("Shards.Tabs.General")}
            </TabsTrigger>
            <TabsTrigger value="config" className="gap-2">
              <GearIcon className="size-4" />
              {t("Shards.Tabs.Configuration")}
            </TabsTrigger>
            <TabsTrigger value="variables" className="gap-2">
              <CodeIcon className="size-4" />
              {t("Shards.Tabs.Variables")} ({shard.variables?.length || 0})
            </TabsTrigger>
            <TabsTrigger value="script" className="gap-2">
              <CodeIcon className="size-4" />
              {t("Shards.Tabs.InstallScript")}
            </TabsTrigger>
            <TabsTrigger value="images" className="gap-2">
              <BoxIcon className="size-4" />
              {t("Shards.Tabs.DockerImages")}
            </TabsTrigger>
          </TabsList>
        </div>

        {/* TAB 1: ALLGEMEIN */}
        <TabsContent value="general">
          <Card className="p-5 lg:p-6 space-y-5">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <Label>{t("Shards.General.Name")}</Label>
                <Input {...form.register("name", { required: true })} className="mt-1.5" />
              </div>
              <div>
                <Label>{t("Shards.General.Collection")}</Label>
                <select
                  {...form.register("collectionId", { required: true })}
                  className="mt-1.5 flex h-11 w-full rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
                >
                  {collections.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <Label>{t("Shards.General.Author")}</Label>
                <Input {...form.register("author")} className="mt-1.5" />
              </div>
              <div>
                <Label>{t("Shards.General.Version")}</Label>
                <Input {...form.register("version")} className="mt-1.5" />
              </div>
            </div>

            <div>
              <Label>{t("Common.Description")}</Label>
              <Textarea
                {...form.register("description")}
                className="mt-1.5 resize-none h-24"
              />
            </div>
          </Card>
        </TabsContent>

        {/* TAB 2: KONFIGURATION */}
        <TabsContent value="config">
          <div className="space-y-6">
            <Card className="p-5 lg:p-6 space-y-5">
              <div>
                <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">Startup & Shutdown Control</h3>
                <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Parameters defining process lifecycle and launch flags.</p>
              </div>

              {/* Startup Command */}
              <div>
                <Label>{t("Shards.Config.StartupCommand")}</Label>
                <Input
                  {...form.register("startupCommand", { required: true })}
                  className="mt-1.5 font-mono text-sm text-brand-600 dark:text-brand-400"
                />
                <p className="mt-1.5 text-xs text-gray-500 dark:text-gray-400">{t("Shards.Config.StartupCommandHint")}</p>

                {/* Live Placeholders Badges */}
                {uniquePlaceholders.length > 0 && (
                  <div className="mt-3 flex flex-wrap items-center gap-1.5 pt-2 border-t border-gray-100 dark:border-white/5">
                    <span className="text-xs text-gray-500 dark:text-gray-400 mr-1">Detected placeholders:</span>
                    {uniquePlaceholders.map((ph) => {
                      const isSystem = systemEnvVars.has(ph.toUpperCase());
                      const isDefined = definedEnvVars.has(ph.toUpperCase());
                      return (
                        <Badge
                          key={ph}
                          variant="light"
                          color={isSystem ? "info" : isDefined ? "success" : "error"}
                          size="sm"
                        >
                          {`{{${ph}}}`}
                          {isSystem ? " (System)" : isDefined ? " (Defined)" : " (Unknown!)"}
                        </Badge>
                      );
                    })}
                  </div>
                )}
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2">
                <div>
                  <Label>{t("Shards.Config.StopCommand")}</Label>
                  <Input {...form.register("stopCommand")} placeholder="stop" className="mt-1.5" />
                  <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("Shards.Config.StopCommandHint")}</p>
                </div>

                <div>
                  <Label>{t("Shards.Config.StopSignal")}</Label>
                  <select
                    {...form.register("stopSignal")}
                    className="mt-1.5 flex h-11 w-full rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
                  >
                    <option value="SIGTERM">SIGTERM (Default)</option>
                    <option value="SIGINT">SIGINT (Ctrl+C)</option>
                    <option value="SIGKILL">SIGKILL (Force kill)</option>
                  </select>
                </div>
              </div>

              <div>
                <Label>{t("Shards.Config.StartupDone")}</Label>
                <Input
                  {...form.register("startupDone")}
                  placeholder=") Done ("
                  className="mt-1.5 font-mono text-xs"
                />
                <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("Shards.Config.StartupDoneHint")}</p>
              </div>
            </Card>

            {/* Config Files */}
            <Card className="p-5 lg:p-6 space-y-4">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div>
                  <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">{t("Shards.Config.ConfigFiles")}</h3>
                  <p className="text-xs text-gray-500 dark:text-gray-400">Files automatically patched before server startup.</p>
                </div>
                <Button size="sm" onClick={handleOpenAddConfigFile} className="w-full sm:w-auto">
                  <PlusIcon className="size-4 mr-1.5" />
                  {t("Shards.Config.AddConfigFile")}
                </Button>
              </div>

              <div className="space-y-2">
                {form.watch("configFiles")?.length === 0 ? (
                  <p className="text-xs text-gray-500 dark:text-gray-400 py-4 text-center border border-dashed border-gray-200 dark:border-gray-800 rounded-xl">
                    No configuration files defined.
                  </p>
                ) : (
                  form.watch("configFiles")?.map((cfg, idx) => (
                    <div
                      key={idx}
                      className="flex items-center justify-between rounded-xl border border-gray-200 bg-gray-50/60 p-3 text-xs dark:border-gray-800 dark:bg-white/[0.02]"
                    >
                      <div className="space-y-0.5">
                        <span className="font-mono font-medium text-brand-600 dark:text-brand-400">{cfg.path}</span>
                        <div className="flex items-center gap-2 text-gray-500 dark:text-gray-400 text-[11px]">
                          <span>Parser: {cfg.parser}</span>
                          <span>•</span>
                          <span>{Object.keys(cfg.mappings || {}).length} replacements</span>
                        </div>
                      </div>
                      <div className="flex items-center gap-1.5">
                        <Button variant="ghost" size="icon-sm" onClick={() => handleOpenEditConfigFile(cfg, idx)}>
                          <PencilIcon className="size-3.5 text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white" />
                        </Button>
                        <Button variant="ghost" size="icon-sm" onClick={() => handleDeleteConfigFile(idx)}>
                          <TrashBinIcon className="size-3.5 text-rose-500 hover:text-rose-600 dark:text-rose-400" />
                        </Button>
                      </div>
                    </div>
                  ))
                )}
              </div>
            </Card>
          </div>
        </TabsContent>

        {/* TAB 3: VARIABLEN */}
        <TabsContent value="variables">
          <Card className="p-5 lg:p-6 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">{t("Shards.VariablesTab.Title")}</h3>
                <p className="text-xs text-gray-500 dark:text-gray-400">{t("Shards.VariablesTab.Subtitle")}</p>
              </div>
              <Button size="sm" onClick={handleOpenAddVariable} className="w-full sm:w-auto">
                <PlusIcon className="size-4 mr-1.5" />
                {t("Shards.VariablesTab.Add")}
              </Button>
            </div>

            <div className="space-y-2.5">
              {!shard.variables || shard.variables.length === 0 ? (
                <div className="py-8 text-center text-xs text-gray-500 dark:text-gray-400 border border-dashed border-gray-200 dark:border-gray-800 rounded-xl">
                  No variables configured for this shard yet.
                </div>
              ) : (
                shard.variables.map((v) => (
                  <div
                    key={v.id}
                    className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 rounded-xl border border-gray-200 bg-gray-50/60 p-4 transition-colors hover:border-gray-300 dark:border-gray-800 dark:bg-white/[0.02] dark:hover:border-gray-700"
                  >
                    <div className="space-y-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-semibold text-gray-800 dark:text-white/90 text-sm">{v.name}</span>
                        <Badge size="sm" variant="light" color="primary" className="font-mono">
                          {v.envVariable}
                        </Badge>
                        {v.defaultValue && (
                          <span className="text-xs text-gray-500 dark:text-gray-400">
                            Default: <span className="font-mono text-gray-700 dark:text-gray-300">{v.defaultValue}</span>
                          </span>
                        )}
                      </div>
                      {v.description && <p className="text-xs text-gray-500 dark:text-gray-400">{v.description}</p>}
                      <div className="flex flex-wrap items-center gap-3 text-[11px] text-gray-400 dark:text-gray-500 pt-1">
                        <span>Rules: <span className="font-mono text-gray-600 dark:text-gray-400">{v.rules}</span></span>
                        <span>•</span>
                        <span>{v.userViewable ? "✓ Visible" : "✗ Hidden"}</span>
                        <span>•</span>
                        <span>{v.userEditable ? "✓ Editable" : "✗ Read-only"}</span>
                      </div>
                    </div>

                    <div className="flex items-center gap-1.5 self-end sm:self-center">
                      <Button variant="ghost" size="icon-sm" onClick={() => handleOpenEditVariable(v)}>
                        <PencilIcon className="size-4 text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white" />
                      </Button>
                      <Button variant="ghost" size="icon-sm" onClick={() => handleDeleteVariable(v)}>
                        <TrashBinIcon className="size-4 text-rose-500 hover:text-rose-600 dark:text-rose-400" />
                      </Button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </Card>
        </TabsContent>

        {/* TAB 4: INSTALL SCRIPT */}
        <TabsContent value="script">
          <Card className="p-5 lg:p-6 space-y-5">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <Label>{t("Shards.ScriptTab.ContainerImage")}</Label>
                <Input
                  {...form.register("installImage")}
                  placeholder="ghcr.io/midnightpanel/installers:alpine"
                  className="mt-1.5 font-mono text-xs"
                />
              </div>
              <div>
                <Label>{t("Shards.ScriptTab.Interpreter")}</Label>
                <Input
                  {...form.register("installInterpreter")}
                  placeholder="/bin/bash"
                  className="mt-1.5 font-mono text-xs"
                />
              </div>
            </div>

            <div>
              <Label className="mb-2 block">{t("Shards.ScriptTab.ScriptCode")}</Label>
              <CodeEditor
                value={form.watch("installScript") || ""}
                onChange={(code) => form.setValue("installScript", code, { shouldDirty: true })}
                language="bash"
                minHeight="340px"
                placeholder="#!/bin/bash\n# Installation script for this server type\n..."
              />
            </div>
          </Card>
        </TabsContent>

        {/* TAB 5: DOCKER IMAGES */}
        <TabsContent value="images">
          <Card className="p-5 lg:p-6 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">{t("Shards.ImagesTab.Title")}</h3>
                <p className="text-xs text-gray-500 dark:text-gray-400">Available container images that the user can select.</p>
              </div>
              <Button size="sm" onClick={() => setImageDialogOpen(true)} className="w-full sm:w-auto">
                <PlusIcon className="size-4 mr-1.5" />
                {t("Shards.ImagesTab.Add")}
              </Button>
            </div>

            <div className="space-y-2">
              {Object.entries(form.watch("dockerImages") || {}).map(([label, uri]) => {
                const isDefault = form.watch("defaultDockerImage") === uri;
                return (
                  <div
                    key={label}
                    className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 rounded-xl border border-gray-200 bg-gray-50/60 p-3.5 text-xs dark:border-gray-800 dark:bg-white/[0.02]"
                  >
                    <div className="space-y-0.5">
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-gray-800 dark:text-white/90">{label}</span>
                        {isDefault && (
                          <Badge size="sm" variant="light" color="success">
                            {t("Shards.ImagesTab.Default")}
                          </Badge>
                        )}
                      </div>
                      <span className="font-mono text-gray-500 dark:text-gray-400 text-[11px] break-all">{uri}</span>
                    </div>

                    <div className="flex items-center gap-2 self-end sm:self-center">
                      {!isDefault && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => form.setValue("defaultDockerImage", uri, { shouldDirty: true })}
                          className="text-xs"
                        >
                          Set as Default
                        </Button>
                      )}
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        onClick={() => handleDeleteDockerImage(label)}
                        className="text-rose-500 hover:text-rose-600 dark:text-rose-400"
                      >
                        <TrashBinIcon className="size-4" />
                      </Button>
                    </div>
                  </div>
                );
              })}
            </div>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Variable Add / Edit Dialog */}
      <Dialog open={variableDialogOpen} onOpenChange={setVariableDialogOpen}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>
              {editingVariableId ? "Edit Variable" : t("Shards.VariablesTab.Add")}
            </DialogTitle>
          </DialogHeader>
          <form onSubmit={variableForm.handleSubmit(handleSaveVariable)} className="space-y-4 py-2">
            <div>
              <Label>{t("Common.Name")}</Label>
              <Input
                {...variableForm.register("name", { required: true })}
                placeholder="e.g. Server Jar File"
                className="mt-1.5"
              />
            </div>
            <div>
              <Label>{t("Shards.VariablesTab.EnvVar")}</Label>
              <Input
                {...variableForm.register("envVariable", { required: true })}
                placeholder="SERVER_JARFILE"
                className="mt-1.5 font-mono text-xs uppercase"
              />
            </div>
            <div>
              <Label>{t("Shards.VariablesTab.DefaultVal")}</Label>
              <Input
                {...variableForm.register("defaultValue")}
                placeholder="server.jar"
                className="mt-1.5 font-mono text-xs"
              />
            </div>
            <div>
              <Label>{t("Shards.VariablesTab.Rules")}</Label>
              <Input
                {...variableForm.register("rules")}
                placeholder="required|string|max:40"
                className="mt-1.5 font-mono text-xs"
              />
              <p className="mt-1 text-[11px] text-gray-500 dark:text-gray-400">Examples: required|numeric|between:1024,65535 or required|string|max:50</p>
            </div>
            <div>
              <Label>{t("Common.Description")}</Label>
              <Textarea
                {...variableForm.register("description")}
                placeholder="Description for the user..."
                className="mt-1.5 resize-none h-18 text-xs"
              />
            </div>
            <div className="flex items-center justify-between pt-2">
              <label className="flex items-center gap-2 cursor-pointer text-xs text-gray-700 dark:text-gray-300">
                <Checkbox
                  checked={variableForm.watch("userViewable")}
                  onCheckedChange={(c) => variableForm.setValue("userViewable", !!c)}
                />
                {t("Shards.VariablesTab.UserViewable")}
              </label>
              <label className="flex items-center gap-2 cursor-pointer text-xs text-gray-700 dark:text-gray-300">
                <Checkbox
                  checked={variableForm.watch("userEditable")}
                  onCheckedChange={(c) => variableForm.setValue("userEditable", !!c)}
                />
                {t("Shards.VariablesTab.UserEditable")}
              </label>
            </div>

            <DialogFooter className="pt-3">
              <Button type="button" variant="outline" onClick={() => setVariableDialogOpen(false)}>
                {t("Common.Cancel")}
              </Button>
              <Button type="submit" disabled={busy}>
                {t("Common.Save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Config File Dialog */}
      <Dialog open={configFileDialogOpen} onOpenChange={setConfigFileDialogOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Configuration File</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>{t("Shards.Config.FilePath")}</Label>
              <Input
                value={cfgPath}
                onChange={(e) => setCfgPath(e.target.value)}
                placeholder="server.properties or config/server.json"
                className="mt-1.5 font-mono text-xs"
              />
            </div>
            <div>
              <Label>{t("Shards.Config.Parser")}</Label>
              <select
                value={cfgParser}
                onChange={(e: React.ChangeEvent<HTMLSelectElement>) => setCfgParser(e.target.value)}
                className="mt-1.5 flex h-11 w-full rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
              >
                <option value="properties">properties (Key=Value)</option>
                <option value="yaml">yaml</option>
                <option value="json">json</option>
                <option value="ini">ini</option>
                <option value="xml">xml</option>
              </select>
            </div>
            <div>
              <Label>{t("Shards.Config.Mappings")}</Label>
              <Textarea
                value={cfgMappingsJson}
                onChange={(e) => setCfgMappingsJson(e.target.value)}
                placeholder='{"server-port": "{{SERVER_PORT}}"}'
                className="mt-1.5 font-mono text-xs h-28"
              />
              <p className="mt-1 text-[11px] text-gray-500 dark:text-gray-400">{t("Shards.Config.MappingsHint")}</p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfigFileDialogOpen(false)}>
              {t("Common.Cancel")}
            </Button>
            <Button onClick={handleSaveConfigFile}>
              {t("Common.Save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Add Docker Image Dialog */}
      <Dialog open={imageDialogOpen} onOpenChange={setImageDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t("Shards.ImagesTab.Add")}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>{t("Shards.ImagesTab.Label")}</Label>
              <Input
                value={newImageLabel}
                onChange={(e) => setNewImageLabel(e.target.value)}
                placeholder="e.g. Java 21 or Node.js 20"
                className="mt-1.5"
              />
            </div>
            <div>
              <Label>{t("Shards.ImagesTab.ImageUri")}</Label>
              <Input
                value={newImageUri}
                onChange={(e) => setNewImageUri(e.target.value)}
                placeholder="ghcr.io/midnightpanel/yolks:java_21"
                className="mt-1.5 font-mono text-xs"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setImageDialogOpen(false)}>
              {t("Common.Cancel")}
            </Button>
            <Button onClick={handleAddDockerImage}>
              {t("Common.Save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </RequirePermission>
  );
}
