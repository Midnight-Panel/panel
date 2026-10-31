import { settingsApi } from "@/api/settings";
import type { AdminSettingsUpdateRequest, FooterLinkItem } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { RequirePermission } from "@/components/shared/guards";
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
      <div className="flex h-72 items-center justify-center text-slate-400">
        Loading system settings...
      </div>
    );
  }

  const currentFooterLinks = form.watch("footerLinks") || [];
  const isUserApiActive = form.watch("userApiEnabled");

  return (
    <RequirePermission permission={Permissions.AdminSystemRead}>
      {/* Top Header */}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl font-bold text-white tracking-tight">System Settings</h1>
            <Badge variant="light" color="primary">
              Core
            </Badge>
          </div>
          <p className="text-xs text-slate-400 mt-0.5">
            Configure system parameters, user access rules, footer links, and support diagnostics.
          </p>
        </div>

        {can(Permissions.AdminSystemWrite) && (
          <Button
            onClick={form.handleSubmit(handleSave)}
            disabled={busy}
            className="bg-indigo-600 hover:bg-indigo-500 text-white shadow-lg shadow-indigo-600/30 self-start sm:self-auto"
          >
            <FloppyDiscIcon className="size-4 mr-1.5" />
            {busy ? "Saving..." : "Save Changes"}
          </Button>
        )}
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
        <TabsList className="bg-[#0F172A] border border-white/10 p-1 rounded-xl">
          <TabsTrigger
            value="general"
            className="gap-2 text-slate-300 data-[state=active]:bg-indigo-600 data-[state=active]:text-white"
          >
            <GearIcon className="size-4" />
            General & Features
          </TabsTrigger>
          <TabsTrigger
            value="api"
            className="gap-2 text-slate-300 data-[state=active]:bg-indigo-600 data-[state=active]:text-white"
          >
            <KeyIcon className="size-4" />
            User API Access
          </TabsTrigger>
          <TabsTrigger
            value="footer"
            className="gap-2 text-slate-300 data-[state=active]:bg-indigo-600 data-[state=active]:text-white"
          >
            <LinkIcon className="size-4" />
            Footer Links ({currentFooterLinks.length})
          </TabsTrigger>
          <TabsTrigger
            value="troubleshooting"
            className="gap-2 text-slate-300 data-[state=active]:bg-rose-600 data-[state=active]:text-white"
          >
            <ArchiveIcon className="size-4" />
            Troubleshooting Bundle
          </TabsTrigger>
        </TabsList>

        {/* TAB 1: GENERAL & FEATURES */}
        <TabsContent value="general">
          <div className="space-y-6">
            <Card className="bg-[#0F172A] border-white/10 p-6 space-y-5 text-white">
              <h3 className="text-base font-semibold text-white">Panel Identity & Routing</h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <Label className="text-slate-300">Panel Application Name</Label>
                  <Input
                    {...form.register("appName", { required: true })}
                    className="mt-1.5 bg-slate-900 border-white/10"
                    placeholder="Midnight"
                  />
                  <p className="mt-1 text-xs text-slate-400">
                    Shown in header, footer, browser title, and outgoing notification emails.
                  </p>
                </div>
                <div>
                  <Label className="text-slate-300">Public URL</Label>
                  <Input
                    {...form.register("publicUrl", { required: true })}
                    className="mt-1.5 font-mono text-xs bg-slate-900 border-white/10"
                    placeholder="http://localhost:5220"
                  />
                  <p className="mt-1 text-xs text-slate-400">
                    Base URL used for daemon connection commands and authentication links.
                  </p>
                </div>
              </div>
            </Card>

            <Card className="bg-[#0F172A] border-white/10 p-6 space-y-5 text-white">
              <h3 className="text-base font-semibold text-white">Feature Toggles</h3>
              <p className="text-xs text-slate-400 -mt-3">
                Enable or disable core panel functionalities globally for all users.
              </p>

              <div className="divide-y divide-white/5 space-y-4">
                {/* Enable Registration */}
                <div className="flex items-center justify-between pt-2">
                  <div className="space-y-0.5 pr-4">
                    <Label className="text-sm font-medium text-slate-200">User Registration</Label>
                    <p className="text-xs text-slate-400">
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
                      <Label className="text-sm font-medium text-slate-200">Maintenance Mode</Label>
                      {form.watch("maintenanceMode") && (
                        <Badge size="sm" variant="light" color="warning">
                          Active
                        </Badge>
                      )}
                    </div>
                    <p className="text-xs text-slate-400">
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
                    <Label className="text-sm font-medium text-slate-200">Server Creation</Label>
                    <p className="text-xs text-slate-400">
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
                    <Label className="text-sm font-medium text-slate-200">Telemetry & Diagnostic Sharing</Label>
                    <p className="text-xs text-slate-400">
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
          <Card className="bg-[#0F172A] border-white/10 p-6 space-y-6 text-white">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-white/5">
              <div>
                <div className="flex items-center gap-2.5">
                  <h3 className="text-base font-semibold text-white">User API Access Control</h3>
                  <Badge
                    size="sm"
                    variant="light"
                    color={isUserApiActive ? "success" : "error"}
                  >
                    {isUserApiActive ? "API Access Enabled" : "API Access Suspended"}
                  </Badge>
                </div>
                <p className="text-xs text-slate-400 mt-1">
                  Manage whether regular panel users are permitted to generate and use personal API keys.
                </p>
              </div>

              <div className="flex items-center gap-3">
                <span className="text-xs text-slate-300 font-medium">
                  {isUserApiActive ? "Enabled" : "Disabled"}
                </span>
                <Switch
                  checked={isUserApiActive}
                  onCheckedChange={(c) => form.setValue("userApiEnabled", c, { shouldDirty: true })}
                />
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="rounded-xl border border-white/5 bg-slate-900/60 p-4 space-y-2">
                <div className="flex items-center gap-2 text-sm font-semibold text-slate-200">
                  <CheckCircleIcon className="size-4 text-emerald-400" />
                  When Enabled
                </div>
                <ul className="text-xs text-slate-400 space-y-1 list-disc pl-4 leading-relaxed">
                  <li>Users can create, view, and revoke API keys from their account settings.</li>
                  <li>Requests authenticated with user API keys execute normally within their permissions.</li>
                  <li>Full automation access for bots and client applications.</li>
                </ul>
              </div>

              <div className="rounded-xl border border-white/5 bg-slate-900/60 p-4 space-y-2">
                <div className="flex items-center gap-2 text-sm font-semibold text-slate-200">
                  <OctagonAlertIcon className="size-4 text-rose-400" />
                  When Disabled
                </div>
                <ul className="text-xs text-slate-400 space-y-1 list-disc pl-4 leading-relaxed">
                  <li>Regular users are prevented from creating new API keys in their account.</li>
                  <li>Existing regular user API keys are immediately blocked with HTTP 403 Forbidden.</li>
                  <li>
                    <strong className="text-slate-300">Administrative override:</strong> Users with administrator privileges retain full API key capabilities.
                  </li>
                </ul>
              </div>
            </div>
          </Card>
        </TabsContent>

        {/* TAB 3: FOOTER LINKS */}
        <TabsContent value="footer">
          <div className="space-y-6">
            <Card className="bg-[#0F172A] border-white/10 p-6 space-y-4 text-white">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div>
                  <h3 className="text-base font-semibold text-white">Footer Links</h3>
                  <p className="text-xs text-slate-400">
                    Custom links displayed on the bottom of every page for all panel users.
                  </p>
                </div>
                <Button size="sm" onClick={handleOpenAddLink} className="bg-indigo-600 hover:bg-indigo-500">
                  <PlusIcon className="size-4 mr-1.5" />
                  Add Footer Link
                </Button>
              </div>

              <div className="space-y-2">
                {currentFooterLinks.length === 0 ? (
                  <div className="py-8 text-center text-xs text-slate-500 border border-dashed border-white/10 rounded-xl">
                    No custom footer links configured. Default links (API Reference & GitHub) will be displayed.
                  </div>
                ) : (
                  <div className="overflow-x-auto rounded-xl border border-white/5">
                    <Table>
                      <TableHeader className="bg-slate-900/80">
                        <TableRow>
                          <TableCell isHeader className="text-xs text-slate-400 py-2.5">
                            Label
                          </TableCell>
                          <TableCell isHeader className="text-xs text-slate-400 py-2.5">
                            URL
                          </TableCell>
                          <TableCell isHeader className="text-xs text-slate-400 py-2.5">
                            Target
                          </TableCell>
                          <TableCell isHeader className="text-xs text-slate-400 py-2.5 text-right">
                            Actions
                          </TableCell>
                        </TableRow>
                      </TableHeader>
                      <TableBody className="divide-y divide-white/5 bg-slate-900/40">
                        {currentFooterLinks.map((link) => (
                          <TableRow key={link.id} className="hover:bg-slate-900/80 transition-colors">
                            <TableCell className="text-xs font-semibold text-white py-3">
                              {link.label}
                            </TableCell>
                            <TableCell className="text-xs font-mono text-slate-300 py-3">
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
                                  className="text-slate-400 hover:text-white"
                                >
                                  <PencilIcon className="size-3.5" />
                                </Button>
                                <Button
                                  variant="ghost"
                                  size="icon-sm"
                                  onClick={() => handleDeleteLink(link)}
                                  className="text-rose-400 hover:text-rose-300"
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
                )}
              </div>

              {/* Live Preview */}
              <div className="rounded-xl border border-white/5 bg-slate-950 p-4 space-y-2 mt-4">
                <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                  Footer Preview
                </span>
                <div className="flex flex-wrap items-center justify-between gap-2 border-t border-white/5 pt-3 text-xs text-slate-400">
                  <span>© {new Date().getFullYear()} {form.watch("appName") || "Midnight Panel"} · v1.0.0</span>
                  <div className="flex flex-wrap gap-4">
                    {currentFooterLinks.length > 0 ? (
                      currentFooterLinks.map((l) => (
                        <span key={l.id} className="text-indigo-400 underline underline-offset-2">
                          {l.label}
                        </span>
                      ))
                    ) : (
                      <>
                        <span className="text-indigo-400">API Reference</span>
                        <span className="text-indigo-400">GitHub</span>
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
          <Card className="bg-[#0F172A] border-rose-500/30 p-6 space-y-6 text-white relative overflow-hidden">
            <div className="absolute top-0 left-0 right-0 h-1 bg-gradient-to-r from-rose-500 via-amber-500 to-indigo-500" />

            <div className="flex items-start gap-4">
              <div className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-rose-500/20 text-rose-400 border border-rose-500/30">
                <OctagonAlertIcon className="size-6" />
              </div>
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  <h3 className="text-lg font-bold text-white tracking-tight">
                    Confidential Support Diagnostic Package
                  </h3>
                  <Badge size="sm" variant="light" color="error">
                    Official Support Only
                  </Badge>
                </div>
                <p className="text-xs text-rose-200/80 leading-relaxed max-w-2xl">
                  Download <code className="font-mono bg-rose-950/80 px-1.5 py-0.5 rounded text-rose-300">troubleshooting.zip</code>.
                  This archive is strictly confidential and designed specifically for the official Midnight Panel development and support team.
                  Only download and share this file if you were explicitly instructed to do so by verified Midnight staff.
                </p>
              </div>
            </div>

            {/* Redaction Notice */}
            <Alert className="border-amber-500/30 bg-amber-950/20 text-amber-200">
              <CheckCircleIcon className="size-4 text-emerald-400 shrink-0" />
              <AlertDescription className="text-xs text-amber-200/90 leading-relaxed">
                <strong>Automatic Privacy Redaction Active:</strong> Before packaging, all sensitive information is strictly sanitized:
                database passwords in connection strings are masked, secret JWT signing keys are redacted, and authentication tokens are removed.
              </AlertDescription>
            </Alert>

            {/* Package Contents Breakdown */}
            <div className="space-y-3">
              <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-400">
                Package Contents Breakdown
              </h4>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                <div className="rounded-xl border border-white/5 bg-slate-900/60 p-3.5 space-y-1">
                  <span className="font-mono font-medium text-indigo-400">config.sanitized.json</span>
                  <p className="text-slate-400 text-[11px]">
                    Current panel configuration with all secrets, database passwords, and private tokens completely masked as <code className="text-amber-300">[REDACTED]</code>.
                  </p>
                </div>
                <div className="rounded-xl border border-white/5 bg-slate-900/60 p-3.5 space-y-1">
                  <span className="font-mono font-medium text-indigo-400">system-info.json</span>
                  <p className="text-slate-400 text-[11px]">
                    OS specs, .NET runtime build, process uptime, memory consumption, database provider, and panel entity counts.
                  </p>
                </div>
                <div className="rounded-xl border border-white/5 bg-slate-900/60 p-3.5 space-y-1">
                  <span className="font-mono font-medium text-indigo-400">diagnostics.json</span>
                  <p className="text-slate-400 text-[11px]">
                    Live database latency test, storage directory permission verification, and online node connectivity status.
                  </p>
                </div>
                <div className="rounded-xl border border-white/5 bg-slate-900/60 p-3.5 space-y-1">
                  <span className="font-mono font-medium text-indigo-400">logs/ & errors_summary.txt</span>
                  <p className="text-slate-400 text-[11px]">
                    The most recent server log files with sanitized bearer tokens, plus an extracted summary of recent errors and warnings.
                  </p>
                </div>
              </div>
            </div>

            {/* Action Download Button */}
            <div className="pt-2 flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-t border-white/5">
              <div className="text-xs text-slate-400">
                File name: <span className="font-mono text-slate-200">troubleshooting.zip</span>
              </div>
              <Button
                onClick={handleDownloadTroubleshooting}
                disabled={downloadingLogs}
                className="bg-rose-600 hover:bg-rose-500 text-white shadow-lg shadow-rose-600/30"
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
        <DialogContent className="sm:max-w-md bg-[#0F172A] border-white/10 text-white">
          <DialogHeader>
            <DialogTitle className="text-lg font-bold">
              {editingLinkId ? "Edit Footer Link" : "Add Footer Link"}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label className="text-slate-300">Link Label</Label>
              <Input
                value={linkForm.label}
                onChange={(e) => setLinkForm((prev) => ({ ...prev, label: e.target.value }))}
                placeholder="e.g. Documentation or Discord"
                className="mt-1.5 bg-slate-900 border-white/10"
              />
            </div>
            <div>
              <Label className="text-slate-300">URL / Destination</Label>
              <Input
                value={linkForm.url}
                onChange={(e) => setLinkForm((prev) => ({ ...prev, url: e.target.value }))}
                placeholder="https://example.com or /api/reference"
                className="mt-1.5 font-mono text-xs bg-slate-900 border-white/10"
              />
            </div>
            <div className="flex items-center justify-between pt-1">
              <Label className="text-slate-300 text-xs cursor-pointer" onClick={() => setLinkForm((p) => ({ ...p, newTab: !p.newTab }))}>
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
            <Button onClick={handleSaveLink} className="bg-indigo-600 hover:bg-indigo-500">
              Save Link
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </RequirePermission>
  );
}
