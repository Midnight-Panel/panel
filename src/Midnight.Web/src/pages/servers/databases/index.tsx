import { ApiError } from "@/api/client";
import { serverDatabasesApi, serverFeatureKeys, type ServerDatabase } from "@/api/server-features";
import { EmptyState } from "@/components/shared/empty-state";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { useT } from "@/i18n";
import { DatabaseIcon, EyeCloseIcon, EyeIcon, LinkIcon, OctagonAlertIcon, PlusIcon, RegenerateIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { ServerPermissions } from "@/lib/server-permissions";
import { copyText } from "@/lib/servers";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import * as z from "zod";
import { CopyButton } from "../components/badges";
import { CardLoader, FeatureCard, Meta, ServerPermissionGate, UsagePill } from "../components/feature-ui";
import { useServerContext } from "../hooks";
import { applyProblemErrors } from "../schema";

/** "s12_" of the existing databases – the server id isn't part of the server detail */
function namePrefix(databases: ServerDatabase[]) {
  for (const database of databases) {
    const match = /^(s\d+_)/.exec(database.name);
    if (match?.[1]) return match[1];
  }

  return null;
}

function CreateDatabaseDialog({ open, prefix, onClose, onNoHost }: {
  open: boolean;
  prefix: string | null;
  onClose: () => void;
  onNoHost: () => void;
}) {
  const { server } = useServerContext();
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const schema = useMemo(
    () =>
      z.object({
        database: z
          .string()
          .trim()
          .min(1, t("Validation.Required"))
          .max(48, t("Servers.Validation.MaxLength", 48))
          .regex(/^[a-zA-Z0-9_]+$/, t("ServerDatabases.NamePattern")),
        remote: z
          .string()
          .trim()
          .min(1, t("Validation.Required"))
          .max(64, t("Servers.Validation.MaxLength", 64))
          .regex(/^[0-9a-zA-Z.%:_\-/]+$/, t("ServerDatabases.RemotePattern")),
      }),
    [t],
  );
  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({ mode: "onTouched", resolver: zodResolver(schema), defaultValues: { database: "", remote: "%" } });
  const { register, control, reset, formState: { errors } } = form;
  const suffix = useWatch({ control, name: "database" });

  const create = async (values: Values) => {
    setBusy(true);

    try {
      const created = await serverDatabasesApi.create(server.uuid, { database: values.database.trim(), remote: values.remote.trim() });
      await queryClient.invalidateQueries({ queryKey: serverFeatureKeys.databases(server.uuid) });
      notifySuccess(t("ServerDatabases.Created", created.name));
      reset();
      onClose();
    } catch (error) {
      // No database host for the node of this server
      if (error instanceof ApiError && error.status === 409 && /host/i.test(error.message)) {
        onNoHost();
        onClose();
        return;
      }

      applyProblemErrors(form.setError, error, { Database: "database", Remote: "remote" });
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(value) => !value && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t("ServerDatabases.Create")}</DialogTitle>
          <DialogDescription>{t("ServerDatabases.CreateHint")}</DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(create)} noValidate className="space-y-5">
          <Field data-invalid={Boolean(errors.database)}>
            <FieldLabel className="mb-1.5 block">{t("ServerDatabases.Name")}</FieldLabel>
            <Input className="font-mono" autoComplete="off" placeholder="minecraft" aria-invalid={errors.database ? "true" : undefined} {...register("database")} />
            <FieldDescription>
              {t("ServerDatabases.NamePreview")}{" "}
              <span className="font-mono text-gray-700 dark:text-gray-300">
                {prefix ?? "s<id>_"}
                {suffix.trim() || "…"}
              </span>
            </FieldDescription>
            <FieldError errors={[errors.database]} />
          </Field>

          <Field data-invalid={Boolean(errors.remote)}>
            <FieldLabel className="mb-1.5 block">{t("ServerDatabases.Remote")}</FieldLabel>
            <Input className="font-mono" autoComplete="off" aria-invalid={errors.remote ? "true" : undefined} {...register("remote")} />
            <FieldDescription>{t("ServerDatabases.RemoteHint")}</FieldDescription>
            <FieldError errors={[errors.remote]} />
          </Field>

          <DialogFooter className="mx-0 mb-0 p-0">
            <Button type="button" variant="outline" onClick={onClose}>{t("Common.Cancel")}</Button>
            <Button type="submit" disabled={busy}>
              {busy ? <Spinner className="size-4" /> : <DatabaseIcon className="size-4" />}
              {t("ServerDatabases.Create")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function PasswordValue({ password }: { password: string }) {
  const { t } = useT();
  const [visible, setVisible] = useState(false);

  return (
    <span className="inline-flex max-w-full items-center gap-1">
      <span className="truncate font-mono text-xs">{visible ? password : "•".repeat(16)}</span>
      <button
        type="button"
        onClick={() => setVisible(!visible)}
        className="inline-flex size-7 shrink-0 items-center justify-center rounded-lg text-gray-500 hover:bg-gray-100 hover:text-gray-800 dark:text-gray-400 dark:hover:bg-white/5 dark:hover:text-white [&_svg]:size-3.5"
        aria-label={visible ? t("ServerDatabases.HidePassword") : t("ServerDatabases.ShowPassword")}
        title={visible ? t("ServerDatabases.HidePassword") : t("ServerDatabases.ShowPassword")}
      >
        {visible ? <EyeCloseIcon /> : <EyeIcon />}
      </button>
      <CopyButton text={password} />
    </span>
  );
}

function DatabaseRow({ database, busy, onRotate, onDelete }: {
  database: ServerDatabase;
  busy: boolean;
  onRotate?: () => void;
  onDelete?: () => void;
}) {
  const { t } = useT();
  const address = `${database.host}:${database.port}`;
  const jdbc = `jdbc:mysql://${address}/${database.name}`;
  const connection = database.password
    ? `mysql://${encodeURIComponent(database.username)}:${encodeURIComponent(database.password)}@${address}/${database.name}`
    : null;

  const copy = async (text: string) => {
    if (await copyText(text)) notifySuccess(t("Common.Copied"));
  };

  return (
    <li className="px-5 py-4 sm:px-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 items-center gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-500/10 text-brand-500 dark:text-brand-400 [&>svg]:size-5">
            <DatabaseIcon />
          </span>
          <div className="min-w-0">
            <p className="flex items-center gap-1 truncate font-mono text-sm font-medium text-gray-800 dark:text-white/90">
              {database.name}
              <CopyButton text={database.name} />
            </p>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-1">
          <Button variant="outline" size="xs" onClick={() => copy(jdbc)}>
            <LinkIcon className="size-3.5" />
            {t("ServerDatabases.CopyJdbc")}
          </Button>
          {connection && (
            <Button variant="outline" size="xs" onClick={() => copy(connection)}>
              <LinkIcon className="size-3.5" />
              {t("ServerDatabases.CopyConnection")}
            </Button>
          )}
          {onRotate && (
            <Button variant="ghost" size="icon-sm" disabled={busy} onClick={onRotate} aria-label={t("ServerDatabases.Rotate")} title={t("ServerDatabases.Rotate")}>
              <RegenerateIcon className="size-4" />
            </Button>
          )}
          {onDelete && (
            <Button
              variant="ghost"
              size="icon-sm"
              className="text-red-500 dark:text-red-400"
              disabled={busy}
              onClick={onDelete}
              aria-label={t("Common.Delete")}
              title={t("Common.Delete")}
            >
              {busy ? <Spinner className="size-4" /> : <TrashBinIcon className="size-4" />}
            </Button>
          )}
        </div>
      </div>

      <dl className="mt-4 grid grid-cols-1 gap-4 rounded-xl border border-gray-100 bg-gray-50/60 p-4 sm:grid-cols-2 lg:grid-cols-4 dark:border-gray-800 dark:bg-white/[0.02]">
        <Meta label={t("ServerDatabases.Endpoint")}>
          <span className="inline-flex items-center gap-1 font-mono text-xs">
            {address}
            <CopyButton text={address} />
          </span>
        </Meta>
        <Meta label={t("Common.Username")}>
          <span className="inline-flex items-center gap-1 font-mono text-xs">
            {database.username}
            <CopyButton text={database.username} />
          </span>
        </Meta>
        <Meta label={t("Common.Password")}>
          {database.password ? <PasswordValue password={database.password} /> : <span className="text-xs text-gray-400 dark:text-gray-500">{t("ServerDatabases.PasswordHidden")}</span>}
        </Meta>
        <Meta label={t("ServerDatabases.Remote")}>
          <span className="font-mono text-xs">{database.remote === "%" ? t("ServerDatabases.Anywhere") : database.remote}</span>
        </Meta>
      </dl>
    </li>
  );
}

function DatabaseList() {
  const { server, can } = useServerContext();
  const { t } = useT();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [creating, setCreating] = useState(false);
  const [noHost, setNoHost] = useState(false);
  const [busy, setBusy] = useState<number | null>(null);
  const queryKey = serverFeatureKeys.databases(server.uuid);

  const { data, isLoading } = useQuery({ queryKey, queryFn: () => serverDatabasesApi.list(server.uuid) });
  const databases = data?.databases ?? [];
  const limit = data?.limit ?? server.featureLimits.databases;
  const atLimit = databases.length >= limit;

  const rotate = async (database: ServerDatabase) => {
    if (!(await confirm({
      title: t("ServerDatabases.Rotate"),
      message: t("ServerDatabases.RotateConfirm", database.name),
      confirmLabel: t("ServerDatabases.Rotate"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    setBusy(database.id);

    try {
      await serverDatabasesApi.rotatePassword(server.uuid, database.id);
      await queryClient.invalidateQueries({ queryKey });
      notifySuccess(t("ServerDatabases.Rotated"));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const remove = async (database: ServerDatabase) => {
    if (!(await confirm({
      title: t("ServerDatabases.Delete"),
      message: t("ServerDatabases.DeleteConfirm", database.name),
      confirmLabel: t("Common.Delete"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    setBusy(database.id);

    try {
      await serverDatabasesApi.delete(server.uuid, database.id);
      await queryClient.invalidateQueries({ queryKey });
      notifySuccess(t("ServerDatabases.Deleted", database.name));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const createButton = can(ServerPermissions.DatabaseCreate) && limit > 0 && (
    <Button size="sm" disabled={atLimit || server.suspended} onClick={() => setCreating(true)} title={atLimit ? t("ServerDatabases.LimitReached") : undefined}>
      <PlusIcon className="size-4" />
      {t("ServerDatabases.Create")}
    </Button>
  );

  return (
    <div className="space-y-4">
      {noHost && (
        <Alert variant="warning" className="px-4 py-3">
          <OctagonAlertIcon />
          <AlertTitle>{t("ServerDatabases.NoHost")}</AlertTitle>
          <AlertDescription>{t("ServerDatabases.NoHostHint")}</AlertDescription>
        </Alert>
      )}

      {isLoading ? (
        <FeatureCard title={t("ServerDatabases.Title")}><CardLoader /></FeatureCard>
      ) : limit <= 0 && databases.length === 0 ? (
        <EmptyState icon={<DatabaseIcon />} title={t("ServerDatabases.Disabled")} description={t("ServerDatabases.DisabledHint")} />
      ) : databases.length === 0 ? (
        <EmptyState icon={<DatabaseIcon />} title={t("ServerDatabases.Empty")} description={t("ServerDatabases.EmptyHint", limit)}>
          {createButton && <div className="mt-6">{createButton}</div>}
        </EmptyState>
      ) : (
        <FeatureCard
          title={t("ServerDatabases.Title")}
          description={t("ServerDatabases.Description")}
          actions={
            <>
              <UsagePill used={databases.length} limit={limit} />
              {createButton}
            </>
          }
        >
          <ul className="divide-y divide-gray-100 dark:divide-white/5">
            {databases.map((database) => (
              <DatabaseRow
                key={database.id}
                database={database}
                busy={busy === database.id}
                onRotate={can(ServerPermissions.DatabaseUpdate) && !server.suspended ? () => rotate(database) : undefined}
                onDelete={can(ServerPermissions.DatabaseDelete) && !server.suspended ? () => remove(database) : undefined}
              />
            ))}
          </ul>
        </FeatureCard>
      )}

      <CreateDatabaseDialog open={creating} prefix={namePrefix(databases)} onClose={() => setCreating(false)} onNoHost={() => setNoHost(true)} />
    </div>
  );
}

export default function ServerDatabasesPage() {
  return (
    <ServerPermissionGate permission={ServerPermissions.DatabaseRead}>
      <DatabaseList />
    </ServerPermissionGate>
  );
}
