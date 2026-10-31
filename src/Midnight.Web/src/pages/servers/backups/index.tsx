import { backupsApi, serverFeatureKeys, startDownload, type Backup } from "@/api/server-features";
import type { ConsoleServerMessage } from "@/api/types";
import { EmptyState } from "@/components/shared/empty-state";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { useT } from "@/i18n";
import { ArchiveIcon, DownloadIcon, HorizontalDotsIcon, LockIcon, OctagonAlertIcon, PlusIcon, RegenerateIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { formatBytes } from "@/lib/format";
import { notifyError, notifySuccess } from "@/lib/notify";
import { ServerPermissions } from "@/lib/server-permissions";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import * as z from "zod";
import { CardLoader, FeatureCard, RelativeTime, ServerPermissionGate, UsagePill } from "../components/feature-ui";
import { useServerContext } from "../hooks";
import { applyProblemErrors } from "../schema";

const IGNORE_PLACEHOLDER = "# One pattern per line, like .gitignore\nlogs/\n*.log\n/cache\n!important.log";

function CreateBackupDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { server } = useServerContext();
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const schema = useMemo(
    () =>
      z.object({
        name: z.string().max(191, t("Servers.Validation.MaxLength", 191)),
        ignoredFiles: z.string().max(8192, t("Servers.Validation.MaxLength", 8192)),
        isLocked: z.boolean(),
      }),
    [t],
  );
  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { name: "", ignoredFiles: "", isLocked: false } });
  const { register, control, reset, formState: { errors } } = form;

  const create = async (values: Values) => {
    setBusy(true);

    try {
      await backupsApi.create(server.uuid, {
        name: values.name.trim() || null,
        ignoredFiles: values.ignoredFiles.trim() || null,
        isLocked: values.isLocked,
      });
      await queryClient.invalidateQueries({ queryKey: serverFeatureKeys.backups(server.uuid) });
      notifySuccess(t("ServerBackups.Started"));
      reset();
      onClose();
    } catch (error) {
      applyProblemErrors(form.setError, error, { Name: "name", IgnoredFiles: "ignoredFiles" });
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(value) => !value && onClose()}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{t("ServerBackups.Create")}</DialogTitle>
          <DialogDescription>{t("ServerBackups.CreateHint")}</DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(create)} noValidate className="space-y-5">
          <Field data-invalid={Boolean(errors.name)}>
            <FieldLabel className="mb-1.5 block">{t("Common.Name")}</FieldLabel>
            <Input placeholder={t("ServerBackups.NamePlaceholder")} aria-invalid={errors.name ? "true" : undefined} {...register("name")} />
            <FieldError errors={[errors.name]} />
          </Field>

          <Field data-invalid={Boolean(errors.ignoredFiles)}>
            <FieldLabel className="mb-1.5 block">{t("ServerBackups.IgnoredFiles")}</FieldLabel>
            <Textarea rows={6} spellCheck={false} className="font-mono text-xs" placeholder={IGNORE_PLACEHOLDER} {...register("ignoredFiles")} />
            <FieldDescription>{t("ServerBackups.IgnoredFilesHint")}</FieldDescription>
            <FieldError errors={[errors.ignoredFiles]} />
          </Field>

          <Controller
            control={control}
            name="isLocked"
            render={({ field }) => (
              <label className="flex cursor-pointer items-start justify-between gap-4 rounded-xl border border-gray-200 p-4 dark:border-gray-800">
                <span>
                  <span className="block text-sm font-medium text-gray-800 dark:text-white/90">{t("ServerBackups.Lock")}</span>
                  <span className="mt-0.5 block text-xs text-gray-500 dark:text-gray-400">{t("ServerBackups.LockHint")}</span>
                </span>
                <Switch checked={field.value} onCheckedChange={field.onChange} />
              </label>
            )}
          />

          <DialogFooter className="mx-0 mb-0 p-0">
            <Button type="button" variant="outline" onClick={onClose}>{t("Common.Cancel")}</Button>
            <Button type="submit" disabled={busy}>
              {busy ? <Spinner className="size-4" /> : <ArchiveIcon className="size-4" />}
              {t("ServerBackups.Create")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function RestoreDialog({ backup, onClose }: { backup: Backup | null; onClose: () => void }) {
  const { server } = useServerContext();
  const { t } = useT();
  const [truncate, setTruncate] = useState(false);
  const [busy, setBusy] = useState(false);

  const restore = async () => {
    if (!backup) return;
    setBusy(true);

    try {
      await backupsApi.restore(server.uuid, backup.uuid, { truncate });
      notifySuccess(t("ServerBackups.RestoreStarted"));
      setTruncate(false);
      onClose();
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={backup !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t("ServerBackups.RestoreTitle")}</DialogTitle>
          <DialogDescription>{t("ServerBackups.RestoreHint", backup?.name ?? "")}</DialogDescription>
        </DialogHeader>

        <Alert variant="warning" className="px-4 py-3">
          <OctagonAlertIcon />
          <AlertTitle>{t("ServerBackups.RestoreWarningTitle")}</AlertTitle>
          <AlertDescription>{t("ServerBackups.RestoreWarning")}</AlertDescription>
        </Alert>

        <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-gray-200 p-4 dark:border-gray-800">
          <Checkbox className="mt-0.5" checked={truncate} onCheckedChange={(checked) => setTruncate(Boolean(checked))} />
          <span>
            <span className="block text-sm font-medium text-gray-800 dark:text-white/90">{t("ServerBackups.Truncate")}</span>
            <span className="mt-0.5 block text-xs text-gray-500 dark:text-gray-400">{t("ServerBackups.TruncateHint")}</span>
          </span>
        </label>

        <DialogFooter className="mx-0 mb-0 p-0 pt-2">
          <Button variant="outline" onClick={onClose}>{t("Common.Cancel")}</Button>
          <Button variant="destructive" disabled={busy} onClick={restore}>
            <RegenerateIcon className="size-4" />
            {t("ServerBackups.Restore")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function BackupStatus({ backup }: { backup: Backup }) {
  const { t } = useT();

  if (!backup.completedAt)
    return (
      <Badge size="sm" color="info">
        <Spinner className="size-3" />
        {t("ServerBackups.Running")}
      </Badge>
    );

  if (!backup.isSuccessful) return <Badge size="sm" color="error">{t("ServerBackups.Failed")}</Badge>;

  return (
    <Tooltip>
      <TooltipTrigger render={<span className="cursor-default" />}>
        <Badge size="sm" color="success">{t("ServerBackups.Completed")}</Badge>
      </TooltipTrigger>
      <TooltipContent className="max-w-xs break-all">
        <span className="block font-medium">{t("ServerBackups.Checksum")}</span>
        <span className="font-mono text-[11px]">{backup.checksum ? `sha256:${backup.checksum}` : "-"}</span>
      </TooltipContent>
    </Tooltip>
  );
}

function BackupList() {
  const { server, can, connection, live } = useServerContext();
  const { t } = useT();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [creating, setCreating] = useState(false);
  const [restoring, setRestoring] = useState<Backup | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const queryKey = serverFeatureKeys.backups(server.uuid);
  const connected = live.connection === "connected";

  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => backupsApi.list(server.uuid),
    // backup-completed refreshes the list over the websocket, polling is the fallback
    refetchInterval: (query) => (query.state.data?.backups.some((backup) => !backup.completedAt) ? (connected ? 30_000 : 5_000) : false),
  });

  useEffect(
    () =>
      connection.subscribeMessages((message: ConsoleServerMessage) => {
        if (message.event === "backup-completed") {
          void queryClient.invalidateQueries({ queryKey: serverFeatureKeys.backups(server.uuid) });
          if (!message.data.successful) notifyError(t("ServerBackups.FailedToast"));
        }

        if (message.event === "backup-restore-completed") {
          if (message.data?.successful === false) notifyError(t("ServerBackups.RestoreFailed"));
          else notifySuccess(t("ServerBackups.RestoreCompleted"));
        }
      }),
    [connection, queryClient, server.uuid, t],
  );

  const backups = data?.backups ?? [];
  const limit = data?.limit ?? server.featureLimits.backups;
  const atLimit = backups.length >= limit;

  const run = async (backup: Backup, action: () => Promise<void>) => {
    setBusy(backup.uuid);

    try {
      await action();
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const download = (backup: Backup) =>
    run(backup, async () => {
      const ticket = await backupsApi.downloadTicket(server.uuid, backup.uuid);
      startDownload(ticket.url);
    });

  const toggleLock = (backup: Backup) =>
    run(backup, async () => {
      await backupsApi.toggleLock(server.uuid, backup.uuid);
      await queryClient.invalidateQueries({ queryKey });
      notifySuccess(backup.isLocked ? t("ServerBackups.Unlocked") : t("ServerBackups.Locked"));
    });

  const remove = async (backup: Backup) => {
    if (!(await confirm({
      title: t("ServerBackups.Delete"),
      message: t("ServerBackups.DeleteConfirm", backup.name),
      confirmLabel: t("Common.Delete"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    await run(backup, async () => {
      await backupsApi.delete(server.uuid, backup.uuid);
      await queryClient.invalidateQueries({ queryKey });
      notifySuccess(t("ServerBackups.Deleted"));
    });
  };

  const createButton = can(ServerPermissions.BackupCreate) && limit > 0 && (
    <Button size="sm" disabled={atLimit || server.suspended} onClick={() => setCreating(true)} title={atLimit ? t("ServerBackups.LimitReached") : undefined}>
      <PlusIcon className="size-4" />
      {t("ServerBackups.Create")}
    </Button>
  );

  const actions = (backup: Backup) => {
    const running = !backup.completedAt;
    const usable = !running && backup.isSuccessful;
    const items = [
      can(ServerPermissions.BackupDownload) && usable && (
        <DropdownMenuItem key="download" onClick={() => download(backup)}>
          <DownloadIcon className="size-4" />
          {t("ServerBackups.Download")}
        </DropdownMenuItem>
      ),
      can(ServerPermissions.BackupRestore) && usable && (
        <DropdownMenuItem key="restore" disabled={server.suspended} onClick={() => setRestoring(backup)}>
          <RegenerateIcon className="size-4" />
          {t("ServerBackups.Restore")}
        </DropdownMenuItem>
      ),
      can(ServerPermissions.BackupDelete) && (
        <DropdownMenuItem key="lock" disabled={running} onClick={() => toggleLock(backup)}>
          <LockIcon className="size-4" />
          {backup.isLocked ? t("ServerBackups.UnlockAction") : t("ServerBackups.LockAction")}
        </DropdownMenuItem>
      ),
    ].filter(Boolean);

    const canDelete = can(ServerPermissions.BackupDelete);

    if (items.length === 0 && !canDelete) return null;

    return (
      <DropdownMenu>
        <DropdownMenuTrigger
          render={<Button variant="ghost" size="icon-sm" disabled={busy === backup.uuid} aria-label={t("ServerFeatures.Actions")} />}
        >
          {busy === backup.uuid ? <Spinner className="size-4" /> : <HorizontalDotsIcon className="size-4" />}
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-52 p-2">
          {items}
          {canDelete && (
            <>
              {items.length > 0 && <DropdownMenuSeparator />}
              <DropdownMenuItem variant="destructive" disabled={backup.isLocked || running} onClick={() => remove(backup)}>
                <TrashBinIcon className="size-4" />
                {backup.isLocked ? t("ServerBackups.DeleteLocked") : t("Common.Delete")}
              </DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
    );
  };

  return (
    <>
      {isLoading ? (
        <FeatureCard title={t("ServerBackups.Title")}><CardLoader /></FeatureCard>
      ) : limit <= 0 && backups.length === 0 ? (
        <EmptyState icon={<ArchiveIcon />} title={t("ServerBackups.Disabled")} description={t("ServerBackups.DisabledHint")} />
      ) : backups.length === 0 ? (
        <EmptyState icon={<ArchiveIcon />} title={t("ServerBackups.Empty")} description={t("ServerBackups.EmptyHint", limit)}>
          {createButton && <div className="mt-6">{createButton}</div>}
        </EmptyState>
      ) : (
        <FeatureCard
          title={t("ServerBackups.Title")}
          description={t("ServerBackups.Description")}
          actions={
            <>
              <UsagePill used={backups.length} limit={limit} />
              {createButton}
            </>
          }
        >
          <ul className="divide-y divide-gray-100 dark:divide-white/5">
            {backups.map((backup) => (
              <li key={backup.uuid} className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:px-6">
                <div className="flex min-w-0 flex-1 items-center gap-3">
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-500/10 text-brand-500 dark:text-brand-400 [&>svg]:size-5">
                    <ArchiveIcon />
                  </span>
                  <div className="min-w-0">
                    <p className="flex items-center gap-2 truncate text-sm font-medium text-gray-800 dark:text-white/90">
                      <span className="truncate">{backup.name}</span>
                      {backup.isLocked && (
                        <Badge size="sm" color="warning">
                          <LockIcon />
                          {t("ServerBackups.LockedBadge")}
                        </Badge>
                      )}
                    </p>
                    <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">
                      <RelativeTime value={backup.createdAt} />
                      {backup.completedAt && backup.isSuccessful && <> · {formatBytes(backup.bytes)}</>}
                    </p>
                  </div>
                </div>
                <div className="flex items-center justify-between gap-3 sm:justify-end">
                  <BackupStatus backup={backup} />
                  {actions(backup)}
                </div>
              </li>
            ))}
          </ul>
        </FeatureCard>
      )}

      <CreateBackupDialog open={creating} onClose={() => setCreating(false)} />
      <RestoreDialog backup={restoring} onClose={() => setRestoring(null)} />
    </>
  );
}

export default function ServerBackupsPage() {
  return (
    <ServerPermissionGate permission={ServerPermissions.BackupRead}>
      <BackupList />
    </ServerPermissionGate>
  );
}
