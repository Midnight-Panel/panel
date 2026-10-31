import { databaseHostsApi, type DatabaseHost, type DatabaseHostTestResult } from "@/api/database-hosts";
import { adminServersApi } from "@/api/servers";
import { tableStyles } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { RequirePermission } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { PasswordInput } from "@/components/shared/password-input";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHeader, TableRow } from "@/components/ui/table";
import { useAuth } from "@/auth/AuthContext";
import { useT } from "@/i18n";
import { CheckCircleIcon, DatabaseIcon, OctagonAlertIcon, PencilIcon, PlusIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { problemFieldErrors } from "@/lib/servers";
import { cn } from "@/utils";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import * as z from "zod";

const queryKey = ["admin", "database-hosts"] as const;
const ALL_NODES = "all";

function TestResult({ result }: { result: DatabaseHostTestResult }) {
  const { t } = useT();

  return result.success ? (
    <Alert variant="success" className="px-4 py-3">
      <CheckCircleIcon />
      <AlertTitle>{t("DatabaseHosts.TestSuccess")}</AlertTitle>
      <AlertDescription>{result.version ? t("DatabaseHosts.TestVersion", result.version) : t("DatabaseHosts.TestSuccessHint")}</AlertDescription>
    </Alert>
  ) : (
    <Alert variant="destructive" className="px-4 py-3">
      <OctagonAlertIcon />
      <AlertTitle>{t("DatabaseHosts.TestFailed")}</AlertTitle>
      <AlertDescription className="break-all">{result.error ?? "-"}</AlertDescription>
    </Alert>
  );
}

function HostDialog({ host, onClose }: { host: DatabaseHost | null; onClose: () => void }) {
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState<"save" | "test" | null>(null);
  const [test, setTest] = useState<DatabaseHostTestResult | null>(null);
  const nodes = useQuery({ queryKey: ["admin", "nodes", "list", "select"], queryFn: adminServersApi.listNodes });

  const schema = useMemo(() => {
    const whole = (min: number, max: number) =>
      z.string().trim().refine((value) => /^\d+$/.test(value) && Number(value) >= min && Number(value) <= max, t("Servers.Validation.Range", min, max));

    return z.object({
      name: z.string().trim().min(1, t("Validation.Required")).max(64, t("Servers.Validation.MaxLength", 64)),
      host: z.string().trim().min(1, t("Validation.Required")).max(255, t("Servers.Validation.MaxLength", 255)),
      port: whole(1, 65535),
      username: z.string().trim().min(1, t("Validation.Required")).max(64, t("Servers.Validation.MaxLength", 64)),
      password: host ? z.string().max(255) : z.string().min(1, t("Validation.Required")).max(255),
      nodeId: z.string(),
      maxDatabases: z.union([z.literal(""), whole(1, 100_000)]),
    });
  }, [t, host]);
  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({
    mode: "onTouched",
    resolver: zodResolver(schema),
    defaultValues: {
      name: host?.name ?? "",
      host: host?.host ?? "",
      port: String(host?.port ?? 3306),
      username: host?.username ?? "",
      password: "",
      nodeId: host?.nodeId != null ? String(host.nodeId) : ALL_NODES,
      maxDatabases: host?.maxDatabases != null ? String(host.maxDatabases) : "",
    },
  });
  const { register, control, formState: { errors, isDirty } } = form;

  const save = async (values: Values) => {
    setBusy("save");

    try {
      const request = {
        name: values.name.trim(),
        host: values.host.trim(),
        port: Number(values.port),
        username: values.username.trim(),
        password: values.password ? values.password : null,
        nodeId: values.nodeId === ALL_NODES ? null : Number(values.nodeId),
        maxDatabases: values.maxDatabases === "" ? null : Number(values.maxDatabases),
      };

      if (host) await databaseHostsApi.update(host.id, request);
      else await databaseHostsApi.create(request);

      await queryClient.invalidateQueries({ queryKey });
      notifySuccess(host ? t("Common.Saved") : t("DatabaseHosts.Created", request.name));
      onClose();
    } catch (error) {
      const fields = problemFieldErrors(error);
      const paths: Record<string, keyof Values> = { name: "name", host: "host", port: "port", username: "username", password: "password", nodeid: "nodeId", maxdatabases: "maxDatabases" };
      for (const [key, message] of Object.entries(fields)) {
        const path = paths[key.replace(/^\$\./, "").toLowerCase()];
        if (path) form.setError(path, { type: "server", message });
      }
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const runTest = async () => {
    if (!host) return;
    setBusy("test");
    setTest(null);

    try {
      setTest(await databaseHostsApi.test(host.id));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const nodeItems = [{ value: ALL_NODES, label: t("DatabaseHosts.AllNodes") }, ...(nodes.data?.items ?? []).map((node) => ({ value: String(node.id), label: node.name }))];

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{host ? t("DatabaseHosts.Edit", host.name) : t("DatabaseHosts.Create")}</DialogTitle>
          <DialogDescription>{t("DatabaseHosts.DialogHint")}</DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(save)} noValidate className="space-y-5">
          <Field data-invalid={Boolean(errors.name)}>
            <FieldLabel className="mb-1.5 block">{t("Common.Name")}</FieldLabel>
            <Input placeholder="mysql-eu-1" aria-invalid={errors.name ? "true" : undefined} {...register("name")} />
            <FieldError errors={[errors.name]} />
          </Field>

          <div className="grid gap-4 sm:grid-cols-[1fr_8rem]">
            <Field data-invalid={Boolean(errors.host)}>
              <FieldLabel className="mb-1.5 block">{t("DatabaseHosts.Host")}</FieldLabel>
              <Input className="font-mono" placeholder="db.example.com" aria-invalid={errors.host ? "true" : undefined} {...register("host")} />
              <FieldError errors={[errors.host]} />
            </Field>
            <Field data-invalid={Boolean(errors.port)}>
              <FieldLabel className="mb-1.5 block">{t("DatabaseHosts.Port")}</FieldLabel>
              <Input className="font-mono" inputMode="numeric" aria-invalid={errors.port ? "true" : undefined} {...register("port")} />
              <FieldError errors={[errors.port]} />
            </Field>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field data-invalid={Boolean(errors.username)}>
              <FieldLabel className="mb-1.5 block">{t("Common.Username")}</FieldLabel>
              <Input className="font-mono" autoComplete="off" aria-invalid={errors.username ? "true" : undefined} {...register("username")} />
              <FieldError errors={[errors.username]} />
            </Field>
            <Field data-invalid={Boolean(errors.password)}>
              <FieldLabel className="mb-1.5 block">{t("Common.Password")}</FieldLabel>
              <PasswordInput
                autoComplete="new-password"
                placeholder={host ? t("DatabaseHosts.PasswordKeep") : undefined}
                aria-invalid={errors.password ? "true" : undefined}
                {...register("password")}
              />
              <FieldError errors={[errors.password]} />
            </Field>
          </div>
          <p className="-mt-2 text-xs text-gray-500 dark:text-gray-400">{t("DatabaseHosts.UserHint")}</p>

          <div className="grid gap-4 sm:grid-cols-2">
            <Field>
              <FieldLabel className="mb-1.5 block">{t("DatabaseHosts.Node")}</FieldLabel>
              <Controller
                control={control}
                name="nodeId"
                render={({ field }) => (
                  <Select<string> value={field.value} items={nodeItems} onValueChange={(value) => value !== null && field.onChange(value)}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {nodeItems.map((item) => <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>)}
                    </SelectContent>
                  </Select>
                )}
              />
              <FieldDescription>{t("DatabaseHosts.NodeHint")}</FieldDescription>
            </Field>
            <Field data-invalid={Boolean(errors.maxDatabases)}>
              <FieldLabel className="mb-1.5 block">{t("DatabaseHosts.MaxDatabases")}</FieldLabel>
              <Input inputMode="numeric" placeholder={t("Servers.Unlimited")} aria-invalid={errors.maxDatabases ? "true" : undefined} {...register("maxDatabases")} />
              <FieldDescription>{t("DatabaseHosts.MaxDatabasesHint")}</FieldDescription>
              <FieldError errors={[errors.maxDatabases]} />
            </Field>
          </div>

          {test && <TestResult result={test} />}

          <DialogFooter className="mx-0 mb-0 p-0 sm:justify-between">
            {host ? (
              <Button type="button" variant="outline" disabled={busy !== null} onClick={runTest} title={isDirty ? t("DatabaseHosts.TestSavedHint") : undefined}>
                {busy === "test" ? <Spinner className="size-4" /> : <CheckCircleIcon className="size-4" />}
                {t("DatabaseHosts.Test")}
              </Button>
            ) : (
              <span className="self-center text-xs text-gray-500 dark:text-gray-400">{t("DatabaseHosts.TestAfterSave")}</span>
            )}
            <div className="flex flex-col-reverse gap-3 sm:flex-row">
              <Button type="button" variant="outline" onClick={onClose}>{t("Common.Cancel")}</Button>
              <Button type="submit" disabled={busy !== null}>{host ? t("Common.SaveChanges") : t("DatabaseHosts.Create")}</Button>
            </div>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function DatabaseHostList() {
  const { t } = useT();
  const { can } = useAuth();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [dialog, setDialog] = useState<{ host: DatabaseHost | null } | null>(null);
  const [testing, setTesting] = useState<number | null>(null);
  const canWrite = can(Permissions.AdminServersWrite);

  const { data, isLoading } = useQuery({ queryKey, queryFn: databaseHostsApi.list });
  const hosts = data ?? [];

  const test = async (host: DatabaseHost) => {
    setTesting(host.id);

    try {
      const result = await databaseHostsApi.test(host.id);
      if (result.success) notifySuccess(result.version ? `${t("DatabaseHosts.TestSuccess")} · ${result.version}` : t("DatabaseHosts.TestSuccess"));
      else notifyError(`${t("DatabaseHosts.TestFailed")}: ${result.error ?? ""}`);
    } catch (error) {
      notifyError(error);
    } finally {
      setTesting(null);
    }
  };

  const remove = async (host: DatabaseHost) => {
    if (!(await confirm({
      title: t("DatabaseHosts.Delete"),
      message: t("DatabaseHosts.DeleteConfirm", host.name),
      confirmLabel: t("Common.Delete"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    try {
      await databaseHostsApi.delete(host.id);
      await queryClient.invalidateQueries({ queryKey });
      notifySuccess(t("DatabaseHosts.Deleted", host.name));
    } catch (error) {
      notifyError(error);
    }
  };

  const createButton = canWrite && (
    <Button onClick={() => setDialog({ host: null })}>
      <PlusIcon />
      {t("DatabaseHosts.Create")}
    </Button>
  );

  return (
    <>
      <PageHeader icon={<DatabaseIcon />} title={t("Nav.Admin.DatabaseHosts")} description={t("DatabaseHosts.Description")} actions={createButton} />

      {isLoading ? (
        <div className="flex justify-center py-20 text-brand-500"><Spinner className="size-8" /></div>
      ) : hosts.length === 0 ? (
        <EmptyState icon={<DatabaseIcon />} title={t("DatabaseHosts.Empty")} description={t("DatabaseHosts.EmptyHint")} />
      ) : (
        <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02]">
          <div className="max-w-full overflow-x-auto">
            <Table>
              <TableHeader className="border-b border-gray-100 dark:border-white/5">
                <TableRow>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("Common.Name")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("DatabaseHosts.Endpoint")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("Common.Username")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("DatabaseHosts.Node")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("DatabaseHosts.Databases")}</TableCell>
                  <TableCell isHeader className={cn(tableStyles.headerCell, "text-end")}>{""}</TableCell>
                </TableRow>
              </TableHeader>
              <TableBody className="divide-y divide-gray-100 dark:divide-white/5">
                {hosts.map((host) => (
                  <TableRow key={host.id}>
                    <TableCell className={cn(tableStyles.bodyCell, "font-medium text-gray-800 dark:text-white/90")}>{host.name}</TableCell>
                    <TableCell className={cn(tableStyles.bodyCell, "font-mono")}>{host.host}:{host.port}</TableCell>
                    <TableCell className={cn(tableStyles.bodyCell, "font-mono")}>{host.username}</TableCell>
                    <TableCell className={tableStyles.bodyCell}>
                      {host.nodeName ? <Badge size="sm">{host.nodeName}</Badge> : <Badge size="sm" color="gray">{t("DatabaseHosts.AllNodes")}</Badge>}
                    </TableCell>
                    <TableCell className={cn(tableStyles.bodyCell, "tabular-nums")}>
                      {host.maxDatabases != null ? t("ServerFeatures.OfLimit", host.databaseCount, host.maxDatabases) : host.databaseCount}
                    </TableCell>
                    <TableCell className={cn(tableStyles.bodyCell, "text-end")}>
                      <span className="inline-flex items-center gap-1">
                        {canWrite && (
                          <>
                            <Button variant="outline" size="xs" disabled={testing !== null} onClick={() => test(host)}>
                              {testing === host.id ? <Spinner className="size-3.5" /> : <CheckCircleIcon className="size-3.5" />}
                              {t("DatabaseHosts.Test")}
                            </Button>
                            <Button variant="ghost" size="icon-sm" onClick={() => setDialog({ host })} aria-label={t("Common.Edit")} title={t("Common.Edit")}>
                              <PencilIcon className="size-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              className="text-red-500 dark:text-red-400"
                              disabled={host.databaseCount > 0}
                              onClick={() => remove(host)}
                              aria-label={t("Common.Delete")}
                              title={host.databaseCount > 0 ? t("DatabaseHosts.DeleteInUse") : t("Common.Delete")}
                            >
                              <TrashBinIcon className="size-4" />
                            </Button>
                          </>
                        )}
                      </span>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </div>
      )}

      {dialog && <HostDialog host={dialog.host} onClose={() => setDialog(null)} />}
    </>
  );
}

export default function DatabaseHostsPage() {
  return (
    <RequirePermission permission={Permissions.AdminServersRead}>
      <DatabaseHostList />
    </RequirePermission>
  );
}
