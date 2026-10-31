import { settingsApi } from "@/api/settings";
import type { AdminSettingsUpdateRequest, FooterLinkItem } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { RequirePermission } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Table, TableBody, TableCell, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  ArchiveIcon,
  CheckCircleIcon,
  DownloadIcon,
  FloppyDiscIcon,
  GearIcon,
  KeyIcon,
  LinkIcon,
  OctagonAlertIcon,
  PencilIcon,
  PlusIcon,
  TrashBinIcon,
} from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";

export default function AdminSettingsPage() {
  const { can, reloadSettings } = useAuth();
  const confirm = useConfirm();
  const queryClient = useQueryClient();

  const [activeTab, setActiveTab] = useState("general");
  const [busy, setBusy] = useState(false);
  const [downloadingLogs, setDownloadingLogs] = useState(false);

  // Footer Link Dialog state
  const [linkDialogOpen, setLinkDialogOpen] = useState(false);
  const [editingLinkId, setEditingLinkId] = useState<string | null>(null);
  const [linkForm, setLinkForm] = useState({ label: "", url: "", newTab: true });

  const { data: settings, isLoading } = useQuery({
    queryKey: ["admin", "settings"],
    queryFn: () => settingsApi.getSettings(),
  });

  const form = useForm<AdminSettingsUpdateRequest>({
    defaultValues: {
      appName: "Midnight",
      publicUrl: "http://localhost:5220",
      enableRegistration: true,
      userApiEnabled: true,
      maintenanceMode: false,
      serverCreationEnabled: true,
      telemetryEnabled: false,
      footerLinks: [],
    },
  });

  useEffect(() => {
    if (settings) {
      form.reset({
        appName: settings.appName,
        publicUrl: settings.publicUrl,
        enableRegistration: settings.enableRegistration,
        userApiEnabled: settings.userApiEnabled,
        maintenanceMode: settings.maintenanceMode,
        serverCreationEnabled: settings.serverCreationEnabled,
        telemetryEnabled: settings.telemetryEnabled,
        footerLinks: settings.footerLinks || [],
      });
    }
  }, [settings, form]);

  const handleSave = async (values: AdminSettingsUpdateRequest) => {
    setBusy(true);
    try {
      await settingsApi.updateSettings(values);
      await queryClient.invalidateQueries({ queryKey: ["admin", "settings"] });
      await reloadSettings();
      notifySuccess("Settings saved successfully.");
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  // Footer link operations
  const handleOpenAddLink = () => {
    setEditingLinkId(null);
    setLinkForm({ label: "", url: "", newTab: true });
    setLinkDialogOpen(true);
  };

  const handleOpenEditLink = (item: FooterLinkItem) => {
    setEditingLinkId(item.id);
    setLinkForm({ label: item.label, url: item.url, newTab: item.newTab });
    setLinkDialogOpen(true);
  };

  const handleSaveLink = () => {
    if (!linkForm.label.trim() || !linkForm.url.trim()) {
      notifyError("Label and URL are required.");
      return;
    }

    const currentLinks = [...(form.getValues("footerLinks") || [])];

    if (editingLinkId) {
      const idx = currentLinks.findIndex((l) => l.id === editingLinkId);
      if (idx !== -1) {
        currentLinks[idx] = {
          id: editingLinkId,
          label: linkForm.label.trim(),
          url: linkForm.url.trim(),
          newTab: linkForm.newTab,
        };
      }
    } else {
      currentLinks.push({
        id: Math.random().toString(36).substring(2, 9),
        label: linkForm.label.trim(),
        url: linkForm.url.trim(),
        newTab: linkForm.newTab,
      });
    }

    form.setValue("footerLinks", currentLinks, { shouldDirty: true });
    setLinkDialogOpen(false);
  };

  const handleDeleteLink = async (item: FooterLinkItem) => {
    if (
      !(await confirm({
        title: "Delete Footer Link",
        message: `Are you sure you want to delete the footer link "${item.label}"?`,
        confirmLabel: "Delete",
        cancelLabel: "Cancel",
      }))
    ) {
      return;
    }

    const currentLinks = (form.getValues("footerLinks") || []).filter((l) => l.id !== item.id);
    form.setValue("footerLinks", currentLinks, { shouldDirty: true });
  };

  const handleDownloadTroubleshooting = async () => {
    setDownloadingLogs(true);
    try {
      await settingsApi.downloadTroubleshooting();
      notifySuccess("Troubleshooting bundle downloaded successfully.");
    } catch (err) {
      notifyError(err);
    } finally {
      setDownloadingLogs(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex h-72 items-center justify-center text-gray-500 dark:text-gray-400">
        Loading system settings...
      </div>
    );
  }

  const currentFooterLinks = form.watch("footerLinks") || [];
  const isUserApiActive = form.watch("userApiEnabled");

  return (
    <RequirePermission permission={Permissions.AdminSystemRead}>
      {/* Top Header */}
      <PageHeader
        title="System Settings"
        description="Configure system parameters, user access rules, footer links, and support diagnostics."
        actions={
          can(Permissions.AdminSystemWrite) && (
            <Button
              onClick={form.handleSubmit(handleSave)}
              disabled={busy}
              className="w-full sm:w-auto"
            >
              <FloppyDiscIcon className="size-4 mr-1.5" />
              {busy ? "Saving..." : "Save Changes"}
            </Button>
          )
        }
      />

      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
        <div className="overflow-x-auto no-scrollbar pb-1 -mx-4 px-4 sm:mx-0 sm:px-0">
          <TabsList className="inline-flex w-auto min-w-full sm:min-w-0">
            <TabsTrigger value="general" className="gap-2">
              <GearIcon className="size-4" />
              General & Features
            </TabsTrigger>
            <TabsTrigger value="api" className="gap-2">
              <KeyIcon className="size-4" />
              User API Access
            </TabsTrigger>
            <TabsTrigger value="footer" className="gap-2">
              <LinkIcon className="size-4" />
              Footer Links ({currentFooterLinks.length})
            </TabsTrigger>
            <TabsTrigger value="troubleshooting" className="gap-2">
              <ArchiveIcon className="size-4" />
              Troubleshooting Bundle
            </TabsTrigger>
          </TabsList>
        </div>

        {/* TAB 1: GENERAL & FEATURES */}
        <TabsContent value="general">
          <div className="space-y-6">
            <Card className="p-5 lg:p-6 space-y-5">
              <div>
                <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">Panel Identity & Routing</h3>
                <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">
                  Basic domain and branding identifiers used throughout the panel.
                </p>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <Label>Panel Application Name</Label>
                  <Input
                    {...form.register("appName", { required: true })}
                    className="mt-1.5"
                    placeholder="Midnight"
                  />
                  <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                    Shown in header, footer, browser title, and outgoing notification emails.
                  </p>
                </div>
                <div>
                  <Label>Public URL</Label>
                  <Input
                    {...form.register("publicUrl", { required: true })}
                    className="mt-1.5 font-mono text-xs"
                    placeholder="http://localhost:5220"
                  />
                  <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
                    Base URL used for daemon connection commands and authentication links.
                  </p>
                </div>
              </div>
            </Card>

            <Card className="p-5 lg:p-6 space-y-5">
              <div>
                <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">Feature Toggles</h3>
                <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">
                  Enable or disable core panel functionalities globally for all users.
                </p>
              </div>

              <div className="divide-y divide-gray-100 dark:divide-white/5 space-y-4">
                {/* Enable Registration */}
                <div className="flex items-center justify-between pt-2">
                  <div className="space-y-0.5 pr-4">
                    <Label className="text-sm font-medium text-gray-800 dark:text-white/90">User Registration</Label>
                    <p className="text-xs text-gray-500 dark:text-gray-400">
                      Allow public sign-ups on the registration page. When disabled, accounts can only be created by administrators.
                    </p>
                  </div>
                  <Switch
                    checked={form.watch("enableRegistration")}
                    onCheckedChange={(c) => form.setValue("enableRegistration", c, { shouldDirty: true })}
                  />
                </div>

                {/* Maintenance Mode */}
                <div className="flex items-center justify-between pt-4">
                  <div className="space-y-0.5 pr-4">
                    <div className="flex items-center gap-2">
                      <Label className="text-sm font-medium text-gray-800 dark:text-white/90">Maintenance Mode</Label>
                      {form.watch("maintenanceMode") && (
                        <Badge size="sm" variant="light" color="warning">
                          Active
                        </Badge>
                      )}
                    </div>
                    <p className="text-xs text-gray-500 dark:text-gray-400">
                      Displays a persistent maintenance notice across the panel and warns regular users that operations may be restricted.
                    </p>
                  </div>
                  <Switch
                    checked={form.watch("maintenanceMode")}
                    onCheckedChange={(c) => form.setValue("maintenanceMode", c, { shouldDirty: true })}
                  />
                </div>

                {/* Server Creation */}
                <div className="flex items-center justify-between pt-4">
                  <div className="space-y-0.5 pr-4">
                    <Label className="text-sm font-medium text-gray-800 dark:text-white/90">Server Creation</Label>
                    <p className="text-xs text-gray-500 dark:text-gray-400">
                      Allows non-administrator users to request and deploy new game server instances.
                    </p>
                  </div>
                  <Switch
                    checked={form.watch("serverCreationEnabled")}
                    onCheckedChange={(c) => form.setValue("serverCreationEnabled", c, { shouldDirty: true })}
                  />
                </div>

                {/* Telemetry */}
                <div className="flex items-center justify-between pt-4">
                  <div className="space-y-0.5 pr-4">
                    <Label className="text-sm font-medium text-gray-800 dark:text-white/90">Telemetry & Diagnostic Sharing</Label>
                    <p className="text-xs text-gray-500 dark:text-gray-400">
                      Permits anonymous error collection and performance statistics to help improve Midnight Panel.
                    </p>
                  </div>
                  <Switch
                    checked={form.watch("telemetryEnabled")}
                    onCheckedChange={(c) => form.setValue("telemetryEnabled", c, { shouldDirty: true })}
                  />
                </div>
              </div>
            </Card>
          </div>
        </TabsContent>

        {/* TAB 2: USER API ACCESS */}
        <TabsContent value="api">
          <Card className="p-5 lg:p-6 space-y-6">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-gray-100 dark:border-white/5">
              <div>
                <div className="flex items-center gap-2.5">
                  <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">User API Access Control</h3>
                  <Badge
                    size="sm"
                    variant="light"
                    color={isUserApiActive ? "success" : "error"}
                  >
                    {isUserApiActive ? "API Access Enabled" : "API Access Suspended"}
                  </Badge>
                </div>
                <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  Manage whether regular panel users are permitted to generate and use personal API keys.
                </p>
              </div>

              <div className="flex items-center gap-3">
                <span className="text-xs text-gray-600 dark:text-gray-400 font-medium">
                  {isUserApiActive ? "Enabled" : "Disabled"}
                </span>
                <Switch
                  checked={isUserApiActive}
                  onCheckedChange={(c) => form.setValue("userApiEnabled", c, { shouldDirty: true })}
                />
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="rounded-xl border border-gray-200 bg-gray-50/60 p-4 space-y-2 dark:border-gray-800 dark:bg-white/3">
                <div className="flex items-center gap-2 text-sm font-semibold text-gray-800 dark:text-white/90">
                  <CheckCircleIcon className="size-4 text-emerald-500" />
                  When Enabled
                </div>
                <ul className="text-xs text-gray-600 dark:text-gray-400 space-y-1 list-disc pl-4 leading-relaxed">
                  <li>Users can create, view, and revoke API keys from their account settings.</li>
                  <li>Requests authenticated with user API keys execute normally within their permissions.</li>
                  <li>Full automation access for bots and client applications.</li>
                </ul>
              </div>

              <div className="rounded-xl border border-gray-200 bg-gray-50/60 p-4 space-y-2 dark:border-gray-800 dark:bg-white/3">
                <div className="flex items-center gap-2 text-sm font-semibold text-gray-800 dark:text-white/90">
                  <OctagonAlertIcon className="size-4 text-rose-500" />
                  When Disabled
                </div>
                <ul className="text-xs text-gray-600 dark:text-gray-400 space-y-1 list-disc pl-4 leading-relaxed">
                  <li>Regular users are prevented from creating new API keys in their account.</li>
                  <li>Existing regular user API keys are immediately blocked with HTTP 403 Forbidden.</li>
                  <li>
                    <strong className="text-gray-700 dark:text-gray-300">Administrative override:</strong> Users with administrator privileges retain full API key capabilities.
                  </li>
                </ul>
              </div>
            </div>
          </Card>
        </TabsContent>

        {/* TAB 3: FOOTER LINKS */}
        <TabsContent value="footer">
          <div className="space-y-6">
            <Card className="p-5 lg:p-6 space-y-4">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div>
                  <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">Footer Links</h3>
                  <p className="text-xs text-gray-500 dark:text-gray-400">
                    Custom links displayed on the bottom of every page for all panel users.
                  </p>
                </div>
                <Button size="sm" onClick={handleOpenAddLink} className="w-full sm:w-auto">
                  <PlusIcon className="size-4 mr-1.5" />
                  Add Footer Link
                </Button>
              </div>

              <div className="space-y-2">
                {currentFooterLinks.length === 0 ? (
                  <div className="py-8 text-center text-xs text-gray-500 dark:text-gray-400 border border-dashed border-gray-200 dark:border-gray-800 rounded-xl">
                    No custom footer links configured. Default links (API Reference & GitHub) will be displayed.
                  </div>
                ) : (
                  <>
                    {/* Mobile View: Clean cards */}
                    <div className="space-y-3 sm:hidden">
                      {currentFooterLinks.map((link) => (
                        <div
                          key={link.id}
                          className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-gray-50/60 p-3.5 dark:border-gray-800 dark:bg-white/3"
                        >
                          <div className="flex items-center justify-between gap-2">
                            <span className="text-sm font-semibold text-gray-800 dark:text-white/90">
                              {link.label}
                            </span>
                            <Badge size="sm" variant="light" color={link.newTab ? "info" : "dark"}>
                              {link.newTab ? "New Tab (_blank)" : "Same Tab"}
                            </Badge>
                          </div>
                          <p className="font-mono text-xs text-gray-500 dark:text-gray-400 break-all">
                            {link.url}
                          </p>
                          <div className="flex items-center justify-end gap-2 border-t border-gray-200/60 pt-2 dark:border-gray-800">
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => handleOpenEditLink(link)}
                              className="h-8 gap-1 text-xs"
                            >
                              <PencilIcon className="size-3.5" />
                              Edit
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => handleDeleteLink(link)}
                              className="h-8 gap-1 text-xs text-rose-500 hover:text-rose-600 dark:text-rose-400"
                            >
                              <TrashBinIcon className="size-3.5" />
                              Delete
                            </Button>
                          </div>
                        </div>
                      ))}
                    </div>

                    {/* Desktop View: Table */}
                    <div className="hidden sm:block overflow-x-auto rounded-xl border border-gray-200 dark:border-gray-800">
                      <Table>
                        <TableHeader className="bg-gray-50 dark:bg-gray-900/50">
                          <TableRow>
                            <TableCell isHeader className="text-xs text-gray-500 dark:text-gray-400 py-2.5">
                              Label
                            </TableCell>
                            <TableCell isHeader className="text-xs text-gray-500 dark:text-gray-400 py-2.5">
                              URL
                            </TableCell>
                            <TableCell isHeader className="text-xs text-gray-500 dark:text-gray-400 py-2.5">
                              Target
                            </TableCell>
                            <TableCell isHeader className="text-xs text-gray-500 dark:text-gray-400 py-2.5 text-right">
                              Actions
                            </TableCell>
                          </TableRow>
                        </TableHeader>
                        <TableBody className="divide-y divide-gray-100 dark:divide-white/5">
                          {currentFooterLinks.map((link) => (
                            <TableRow key={link.id} className="hover:bg-gray-50/50 dark:hover:bg-white/3 transition-colors">
                              <TableCell className="text-xs font-semibold text-gray-800 dark:text-white/90 py-3">
                                {link.label}
                              </TableCell>
                              <TableCell className="text-xs font-mono text-gray-500 dark:text-gray-400 py-3">
                                {link.url}
                              </TableCell>
                              <TableCell className="text-xs py-3">
                                <Badge size="sm" variant="light" color={link.newTab ? "info" : "dark"}>
                                  {link.newTab ? "New Tab (_blank)" : "Same Tab"}
                                </Badge>
                              </TableCell>
                              <TableCell className="text-xs py-3 text-right">
                                <div className="flex items-center justify-end gap-1.5">
                                  <Button
                                    variant="ghost"
                                    size="icon-sm"
                                    onClick={() => handleOpenEditLink(link)}
                                    className="text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
                                  >
                                    <PencilIcon className="size-3.5" />
                                  </Button>
                                  <Button
                                    variant="ghost"
                                    size="icon-sm"
                                    onClick={() => handleDeleteLink(link)}
                                    className="text-rose-500 hover:text-rose-600 dark:text-rose-400"
                                  >
                                    <TrashBinIcon className="size-3.5" />
                                  </Button>
                                </div>
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    </div>
                  </>
                )}
              </div>

              {/* Live Preview */}
              <div className="rounded-xl border border-gray-200 bg-gray-50/70 p-4 space-y-2 mt-4 dark:border-gray-800 dark:bg-gray-900/50">
                <span className="text-[11px] font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400">
                  Footer Preview
                </span>
                <div className="flex flex-wrap items-center justify-between gap-2 border-t border-gray-200/80 dark:border-gray-800 pt-3 text-xs text-gray-500 dark:text-gray-400">
                  <span>© {new Date().getFullYear()} {form.watch("appName") || "Midnight Panel"} · v1.0.0</span>
                  <div className="flex flex-wrap gap-4">
                    {currentFooterLinks.length > 0 ? (
                      currentFooterLinks.map((l) => (
                        <span key={l.id} className="text-brand-500 dark:text-brand-400 underline underline-offset-2">
                          {l.label}
                        </span>
                      ))
                    ) : (
                      <>
                        <span className="text-brand-500 dark:text-brand-400">API Reference</span>
                        <span className="text-brand-500 dark:text-brand-400">GitHub</span>
                      </>
                    )}
                  </div>
                </div>
              </div>
            </Card>
          </div>
        </TabsContent>

        {/* TAB 4: TROUBLESHOOTING ARCHIVE */}
        <TabsContent value="troubleshooting">
          <Card className="border border-rose-200 bg-white dark:border-rose-900/40 dark:bg-white/3 p-5 lg:p-6 space-y-6 relative overflow-hidden">
            <div className="absolute top-0 left-0 right-0 h-1 bg-gradient-to-r from-rose-500 via-amber-500 to-indigo-500" />

            <div className="flex items-start gap-4">
              <div className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-rose-500/10 text-rose-500 border border-rose-500/20">
                <OctagonAlertIcon className="size-6" />
              </div>
              <div className="space-y-1">
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="text-lg font-bold text-gray-800 dark:text-white/90 tracking-tight">
                    Confidential Support Diagnostic Package
                  </h3>
                  <Badge size="sm" variant="light" color="error">
                    Official Support Only
                  </Badge>
                </div>
                <p className="text-xs text-gray-600 dark:text-gray-400 leading-relaxed max-w-2xl">
                  Download <code className="font-mono bg-rose-50 text-rose-700 dark:bg-rose-950/80 dark:text-rose-300 px-1.5 py-0.5 rounded">troubleshooting.zip</code>.
                  This archive is strictly confidential and designed specifically for the official Midnight Panel development and support team.
                  Only download and share this file if you were explicitly instructed to do so by verified Midnight staff.
                </p>
              </div>
            </div>

            {/* Redaction Notice */}
            <Alert className="border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-500/30 dark:bg-amber-950/20 dark:text-amber-200">
              <CheckCircleIcon className="size-4 text-emerald-500 shrink-0" />
              <AlertDescription className="text-xs text-amber-800 dark:text-amber-200/90 leading-relaxed">
                <strong>Automatic Privacy Redaction Active:</strong> Before packaging, all sensitive information is strictly sanitized:
                database passwords in connection strings are masked, secret JWT signing keys are redacted, and authentication tokens are removed.
              </AlertDescription>
            </Alert>

            {/* Package Contents Breakdown */}
            <div className="space-y-3">
              <h4 className="text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400">
                Package Contents Breakdown
              </h4>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                <div className="rounded-xl border border-gray-200 bg-gray-50/70 p-3.5 space-y-1 dark:border-gray-800 dark:bg-white/3">
                  <span className="font-mono font-medium text-brand-600 dark:text-brand-400">config.sanitized.json</span>
                  <p className="text-gray-500 dark:text-gray-400 text-[11px]">
                    Current panel configuration with all secrets, database passwords, and private tokens completely masked as <code className="text-amber-600 dark:text-amber-300">[REDACTED]</code>.
                  </p>
                </div>
                <div className="rounded-xl border border-gray-200 bg-gray-50/70 p-3.5 space-y-1 dark:border-gray-800 dark:bg-white/3">
                  <span className="font-mono font-medium text-brand-600 dark:text-brand-400">system-info.json</span>
                  <p className="text-gray-500 dark:text-gray-400 text-[11px]">
                    OS specs, .NET runtime build, process uptime, memory consumption, database provider, and panel entity counts.
                  </p>
                </div>
                <div className="rounded-xl border border-gray-200 bg-gray-50/70 p-3.5 space-y-1 dark:border-gray-800 dark:bg-white/3">
                  <span className="font-mono font-medium text-brand-600 dark:text-brand-400">diagnostics.json</span>
                  <p className="text-gray-500 dark:text-gray-400 text-[11px]">
                    Live database latency test, storage directory permission verification, and online node connectivity status.
                  </p>
                </div>
                <div className="rounded-xl border border-gray-200 bg-gray-50/70 p-3.5 space-y-1 dark:border-gray-800 dark:bg-white/3">
                  <span className="font-mono font-medium text-brand-600 dark:text-brand-400">logs/ & errors_summary.txt</span>
                  <p className="text-gray-500 dark:text-gray-400 text-[11px]">
                    The most recent server log files with sanitized bearer tokens, plus an extracted summary of recent errors and warnings.
                  </p>
                </div>
              </div>
            </div>

            {/* Action Download Button */}
            <div className="pt-2 flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-t border-gray-100 dark:border-white/5">
              <div className="text-xs text-gray-500 dark:text-gray-400">
                File name: <span className="font-mono text-gray-700 dark:text-gray-300">troubleshooting.zip</span>
              </div>
              <Button
                onClick={handleDownloadTroubleshooting}
                disabled={downloadingLogs}
                className="bg-rose-600 hover:bg-rose-500 text-white w-full sm:w-auto"
              >
                <DownloadIcon className="size-4 mr-2" />
                {downloadingLogs ? "Packaging diagnostic bundle..." : "Download troubleshooting.zip"}
              </Button>
            </div>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Add / Edit Footer Link Dialog */}
      <Dialog open={linkDialogOpen} onOpenChange={setLinkDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {editingLinkId ? "Edit Footer Link" : "Add Footer Link"}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Link Label</Label>
              <Input
                value={linkForm.label}
                onChange={(e) => setLinkForm((prev) => ({ ...prev, label: e.target.value }))}
                placeholder="e.g. Documentation or Discord"
                className="mt-1.5"
              />
            </div>
            <div>
              <Label>URL / Destination</Label>
              <Input
                value={linkForm.url}
                onChange={(e) => setLinkForm((prev) => ({ ...prev, url: e.target.value }))}
                placeholder="https://example.com or /api/reference"
                className="mt-1.5 font-mono text-xs"
              />
            </div>
            <div className="flex items-center justify-between pt-1">
              <Label className="text-xs cursor-pointer text-gray-600 dark:text-gray-400" onClick={() => setLinkForm((p) => ({ ...p, newTab: !p.newTab }))}>
                Open in new browser tab (_blank)
              </Label>
              <Switch
                checked={linkForm.newTab}
                onCheckedChange={(c) => setLinkForm((prev) => ({ ...prev, newTab: c }))}
              />
            </div>
          </div>
          <DialogFooter className="pt-2">
            <Button variant="outline" onClick={() => setLinkDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleSaveLink}>
              Save Link
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </RequirePermission>
  );
}
