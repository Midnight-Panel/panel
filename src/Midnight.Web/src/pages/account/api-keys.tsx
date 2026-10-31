import { api } from "@/api/client";
import type { ApiKey, CreateApiKeyResponse } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { tableStyles } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { PermissionPicker } from "@/components/shared/permission-picker";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { InputGroup, InputGroupAddon, InputGroupButton, InputGroupInput } from "@/components/ui/input-group";
import { Table, TableBody, TableCell, TableHeader, TableRow } from "@/components/ui/table";
import { useT } from "@/i18n";
import { AlertIcon, CheckIcon, CopyIcon, EyeIcon, KeyIcon, PlusIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError } from "@/lib/notify";
import { PermissionGroups, Permissions } from "@/lib/permissions";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";

interface CreateForm {
  name: string;
  expiresAt: string;
  scopes: string[];
}

export default function ApiKeysPage() {
  const { isImpersonating, can, settings } = useAuth();
  const { t, formatDate } = useT();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [createdKey, setCreatedKey] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [busy, setBusy] = useState(false);

  const keys = useQuery({ queryKey: ["account", "api-keys"], queryFn: () => api.get<ApiKey[]>("/api/account/api-keys"), enabled: !isImpersonating });
  const { register, handleSubmit, reset, watch, setValue, formState: { errors } } = useForm<CreateForm>({ defaultValues: { name: "", expiresAt: "", scopes: [] } });

  // Users can only scope keys to permissions they have themselves
  const groups = PermissionGroups.map((g) => ({ ...g, permissions: g.permissions.filter(can) })).filter((g) => g.permissions.length > 0);

  if (isImpersonating) return <EmptyState icon={<EyeIcon />} title={t("Nav.ApiKeys")} description={t("Security.ImpersonationHint")} />;

  const isApiDisabledForUser = settings.userApiEnabled === false && !can(Permissions.AdminSystemWrite);

  const openDialog = () => {
    reset();
    setCreatedKey(null);
    setCopied(false);
    setOpen(true);
  };

  const create = async (values: CreateForm) => {
    if (values.scopes.length === 0) {
      notifyError(t("ApiKeys.NoScopes"));
      return;
    }

    setBusy(true);

    try {
      const expiresAt = values.expiresAt ? new Date(`${values.expiresAt}T23:59:59`).toISOString() : null;
      const response = await api.post<CreateApiKeyResponse>("/api/account/api-keys", { name: values.name, scopes: values.scopes, expiresAt });
      setCreatedKey(response.key);
      await queryClient.invalidateQueries({ queryKey: ["account", "api-keys"] });
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const remove = async (key: ApiKey) => {
    if (!(await confirm({ title: t("Common.Delete"), message: t("ApiKeys.DeleteConfirm", key.name), confirmLabel: t("Common.Delete"), cancelLabel: t("Common.Cancel") })))
      return;

    try {
      await api.delete(`/api/account/api-keys/${key.id}`);
      await queryClient.invalidateQueries({ queryKey: ["account", "api-keys"] });
    } catch (error) {
      notifyError(error);
    }
  };

  const copy = async () => {
    if (!createdKey) return;
    await navigator.clipboard.writeText(createdKey);
    setCopied(true);
  };

  const tomorrow = new Date(Date.now() + 86_400_000).toISOString().slice(0, 10);

  return (
    <>
      <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02]">
        <div className="flex flex-col gap-3 border-b border-gray-100 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6 dark:border-gray-800">
          <div>
            <h3 className="text-lg font-semibold text-gray-800 dark:text-white/90">{t("ApiKeys.Title")}</h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{t("ApiKeys.Description")}</p>
          </div>
          <Button onClick={openDialog} disabled={isApiDisabledForUser}>
            <PlusIcon />
            {t("ApiKeys.Create")}
          </Button>
        </div>

        {isApiDisabledForUser && (
          <div className="bg-amber-500/10 border-b border-amber-500/20 px-5 py-3 text-xs text-amber-200 flex items-center gap-2">
            <span>User API access is currently disabled by the administrator. Creating new API keys is unavailable.</span>
          </div>
        )}

        {keys.data && keys.data.length === 0 ? (
          <p className="py-12 text-center text-sm text-gray-500 dark:text-gray-400">{t("ApiKeys.Empty")}</p>
        ) : (
          <div className="max-w-full overflow-x-auto">
            <Table>
              <TableHeader className="border-b border-gray-100 dark:border-white/5">
                <TableRow>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("Common.Name")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ApiKeys.Prefix")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ApiKeys.Scopes")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ApiKeys.Expires")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ApiKeys.LastUsed")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{" "}</TableCell>
                </TableRow>
              </TableHeader>
              <TableBody className="divide-y divide-gray-100 dark:divide-white/5">
                {(keys.data ?? []).map((key) => (
                  <TableRow key={key.id}>
                    <TableCell className={tableStyles.bodyCell}>
                      <span className="flex items-center gap-2 font-medium text-gray-800 dark:text-white/90"><KeyIcon className="size-4" />{key.name}</span>
                    </TableCell>
                    <TableCell className={`${tableStyles.bodyCell} font-mono text-xs`}>mdp_{key.prefix}_…</TableCell>
                    <TableCell className={tableStyles.bodyCell}>
                      <div className="flex flex-wrap gap-1">{key.scopes.map((scope) => <Badge key={scope} size="sm" className="font-mono">{scope}</Badge>)}</div>
                    </TableCell>
                    <TableCell className={tableStyles.bodyCell}>
                      {key.expiresAt
                        ? <span className={new Date(key.expiresAt) < new Date() ? "text-red-500" : undefined}>{formatDate(key.expiresAt, false)}</span>
                        : t("ApiKeys.Never")}
                    </TableCell>
                    <TableCell className={tableStyles.bodyCell}>{formatDate(key.lastUsedAt)}</TableCell>
                    <TableCell className={`${tableStyles.bodyCell} text-end`}>
                      <Button variant="ghost" size="icon-sm" className="text-red-500" onClick={() => remove(key)} aria-label={t("Common.Delete")}>
                        <TrashBinIcon />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </div>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{t("ApiKeys.Create")}</DialogTitle>
          </DialogHeader>

          {createdKey ? (
            <div className="space-y-4">
              <Alert variant="warning" className="px-4 py-3">
                <AlertIcon />
                <AlertDescription>{t("ApiKeys.CopyNow")}</AlertDescription>
              </Alert>
              <InputGroup>
                <InputGroupInput readOnly value={createdKey} className="font-mono text-xs" />
                <InputGroupAddon align="inline-end">
                  <InputGroupButton size="icon-xs" onClick={copy} aria-label={t("Common.Copy")}>
                    {copied ? <CheckIcon className="text-green-500" /> : <CopyIcon />}
                  </InputGroupButton>
                </InputGroupAddon>
              </InputGroup>
              <DialogFooter className="mx-0 mb-0 p-0">
                <Button onClick={() => setOpen(false)}>{t("Common.Done")}</Button>
              </DialogFooter>
            </div>
          ) : (
            <form onSubmit={handleSubmit(create)} className="space-y-5" noValidate>
              <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
                <Field data-invalid={Boolean(errors.name)}>
                  <FieldLabel className="mb-1.5 block">{t("Common.Name")}</FieldLabel>
                  <Input aria-invalid={errors.name ? "true" : undefined} {...register("name", { required: t("Validation.Required") })} />
                  <FieldError errors={[errors.name]} />
                </Field>
                <Field>
                  <FieldLabel className="mb-1.5 block">{t("ApiKeys.Expires")}</FieldLabel>
                  <Input type="date" min={tomorrow} {...register("expiresAt")} />
                  <FieldDescription>{t("ApiKeys.ExpiresHint")}</FieldDescription>
                </Field>
              </div>
              <div>
                <FieldLabel className="mb-2 block">{t("ApiKeys.Scopes")}</FieldLabel>
                <div className="max-h-[45vh] overflow-y-auto pe-1">
                  <PermissionPicker value={watch("scopes")} onChange={(value) => setValue("scopes", value)} groups={groups} />
                </div>
              </div>
              <DialogFooter className="mx-0 mb-0 p-0">
                <Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("Common.Cancel")}</Button>
                <Button type="submit" disabled={busy}>{t("ApiKeys.Create")}</Button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
