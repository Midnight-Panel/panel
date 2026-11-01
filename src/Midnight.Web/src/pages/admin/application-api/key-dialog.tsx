import {
  ApplicationAccessLevels,
  ApplicationResources,
  applicationKeysApi,
  type ApplicationAccess,
  type ApplicationKey,
  type ApplicationResource,
  type CreatedApplicationKey,
} from "@/api/application-keys";
import { ApiError } from "@/api/client";
import { useAuth } from "@/auth/AuthContext";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import { notifyError, notifySuccess } from "@/lib/notify";
import { problemFieldErrors } from "@/lib/servers";
import { cn } from "@/utils";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import * as z from "zod";
import { applicationKeysQueryKey, canGrant, fullPermissions, isIpOrCidr, parseIpLines, type FullPermissions } from "./resources";

const MAX_IPS = 32;

function AccessControl({ resource, value, original, onChange, disabled }: {
  resource: ApplicationResource;
  value: ApplicationAccess;
  /** Saved level of an existing key, stays selectable even if the admin couldn't grant it */
  original: ApplicationAccess | undefined;
  onChange: (value: ApplicationAccess) => void;
  disabled: boolean;
}) {
  const { t } = useT();
  const { can } = useAuth();

  return (
    <div role="radiogroup" aria-label={t(`ApplicationApi.Resources.${resource}`)} className="grid grid-cols-3 gap-1 rounded-xl border border-gray-200 bg-gray-100 p-1 dark:border-white/5 dark:bg-white/[0.03]">
      {ApplicationAccessLevels.map((level) => {
        const allowed = canGrant(resource, level, can) || level === original;
        const selected = value === level;

        return (
          <button
            key={level}
            type="button"
            role="radio"
            aria-checked={selected}
            disabled={disabled || !allowed}
            title={allowed ? undefined : t("ApplicationApi.Form.MissingPermission")}
            onClick={() => onChange(level)}
            className={cn(
              "rounded-lg px-2.5 py-1.5 text-xs font-medium whitespace-nowrap transition-all disabled:cursor-not-allowed disabled:opacity-40",
              selected
                ? "bg-white font-semibold text-gray-900 shadow-xs dark:bg-white/10 dark:text-white"
                : "text-gray-600 enabled:hover:text-gray-900 dark:text-gray-400 dark:enabled:hover:text-white",
            )}
          >
            {t(`ApplicationApi.Access.${level}`)}
          </button>
        );
      })}
    </div>
  );
}

export function KeyDialog({ applicationKey, onClose, onCreated }: {
  applicationKey: ApplicationKey | null;
  onClose: () => void;
  onCreated: (created: CreatedApplicationKey) => void;
}) {
  const { t } = useT();
  const { can } = useAuth();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);
  const original = applicationKey ? fullPermissions(applicationKey.permissions) : null;

  const schema = useMemo(() => {
    const access = z.enum(ApplicationAccessLevels);

    return z.object({
      description: z.string().trim().min(1, t("Validation.Required")).max(255, t("Servers.Validation.MaxLength", 255)),
      permissions: z.object({
        Users: access,
        Servers: access,
        Nodes: access,
        Allocations: access,
        Shards: access,
        DatabaseHosts: access,
      }),
      allowedIps: z.string().superRefine((value, context) => {
        const lines = parseIpLines(value);
        const invalid = lines.filter((line) => !isIpOrCidr(line));

        if (invalid.length > 0) context.addIssue({ code: "custom", message: t("ApplicationApi.Form.InvalidIps", invalid.join(", ")) });
        else if (lines.length > MAX_IPS) context.addIssue({ code: "custom", message: t("ApplicationApi.Form.TooManyIps", MAX_IPS) });
      }),
    });
  }, [t]);
  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({
    mode: "onTouched",
    resolver: zodResolver(schema),
    defaultValues: {
      description: applicationKey?.description ?? "",
      permissions: original ?? fullPermissions({}),
      allowedIps: applicationKey?.allowedIps.join("\n") ?? "",
    },
  });
  const { register, control, setValue, watch, formState: { errors } } = form;
  const permissions = watch("permissions");

  /** Sets every row to the level, capped at what the admin may grant */
  const setAll = (level: ApplicationAccess) => {
    const next = Object.fromEntries(
      ApplicationResources.map((resource) => {
        const candidates = ApplicationAccessLevels.slice(0, ApplicationAccessLevels.indexOf(level) + 1).reverse();
        const granted = candidates.find((candidate) => canGrant(resource, candidate, can) || candidate === original?.[resource]) ?? "None";
        return [resource, granted];
      }),
    ) as FullPermissions;

    setValue("permissions", next, { shouldDirty: true });
  };

  const save = async (values: Values) => {
    setBusy(true);

    try {
      const request = {
        description: values.description.trim(),
        permissions: values.permissions,
        allowedIps: parseIpLines(values.allowedIps),
      };

      if (applicationKey) {
        await applicationKeysApi.update(applicationKey.id, request);
        notifySuccess(t("Common.Saved"));
      } else {
        onCreated(await applicationKeysApi.create(request));
      }

      await queryClient.invalidateQueries({ queryKey: applicationKeysQueryKey });
      onClose();
    } catch (error) {
      // Grant errors ("You can't grant ...") are 403s without field errors
      const fields = error instanceof ApiError && error.status === 403 ? {} : problemFieldErrors(error);
      const paths: Record<string, keyof Values> = { description: "description", permissions: "permissions", allowedips: "allowedIps" };
      let mapped = false;

      for (const [key, message] of Object.entries(fields)) {
        const path = paths[key.replace(/^\$\./, "").split(/[.[]/)[0].toLowerCase()];
        if (!path) continue;
        form.setError(path, { type: "server", message });
        mapped = true;
      }

      if (!mapped) notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const permissionsError = errors.permissions?.message;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{applicationKey ? t("ApplicationApi.Edit") : t("ApplicationApi.Create")}</DialogTitle>
          <DialogDescription>
            {applicationKey ? <span className="font-mono">{applicationKey.identifier}</span> : t("ApplicationApi.Form.Hint")}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(save)} noValidate className="space-y-5">
          <Field data-invalid={Boolean(errors.description)}>
            <FieldLabel className="mb-1.5 block">{t("Common.Description")}</FieldLabel>
            <Input placeholder={t("ApplicationApi.Form.DescriptionPlaceholder")} aria-invalid={errors.description ? "true" : undefined} {...register("description")} />
            <FieldError errors={[errors.description]} />
          </Field>

          <div>
            <div className="mb-2 flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
              <div>
                <FieldLabel className="block">{t("ApplicationApi.Permissions")}</FieldLabel>
                <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">{t("ApplicationApi.Form.PermissionsHint")}</p>
              </div>
              <div className="flex shrink-0 items-center gap-1 text-xs">
                <span className="me-1 text-gray-500 dark:text-gray-400">{t("ApplicationApi.Form.SetAll")}</span>
                {ApplicationAccessLevels.map((level) => (
                  <Button key={level} type="button" variant="outline" size="xs" className="h-7 px-2 text-xs" onClick={() => setAll(level)}>
                    {t(`ApplicationApi.Access.${level}`)}
                  </Button>
                ))}
              </div>
            </div>

            <div className="divide-y divide-gray-100 overflow-hidden rounded-2xl border border-gray-200 bg-white dark:divide-white/5 dark:border-gray-800 dark:bg-white/[0.02]">
              {ApplicationResources.map((resource) => {
                const grantable = ApplicationAccessLevels.some((level) => level !== "None" && canGrant(resource, level, can));
                const kept = original?.[resource] && original[resource] !== "None";

                return (
                  <div key={resource} className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-gray-800 dark:text-white/90">{t(`ApplicationApi.Resources.${resource}`)}</p>
                      <p className="text-xs text-gray-500 dark:text-gray-400">
                        {grantable ? t(`ApplicationApi.ResourceHints.${resource}`) : t("ApplicationApi.Form.MissingPermission")}
                      </p>
                    </div>
                    <Controller
                      control={control}
                      name={`permissions.${resource}`}
                      render={({ field }) => (
                        <div className="sm:w-72">
                          <AccessControl
                            resource={resource}
                            value={field.value}
                            original={original?.[resource]}
                            disabled={!grantable && !kept}
                            onChange={(value) => {
                              field.onChange(value);
                            }}
                          />
                        </div>
                      )}
                    />
                  </div>
                );
              })}
            </div>
            {permissionsError && <p className="mt-1.5 text-sm text-red-500 dark:text-red-400">{permissionsError}</p>}
            {Object.values(permissions).every((level) => level === "None") && !permissionsError && (
              <p className="mt-1.5 text-xs text-gray-500 dark:text-gray-400">{t("ApplicationApi.Form.NoneSelected")}</p>
            )}
          </div>

          <Field data-invalid={Boolean(errors.allowedIps)}>
            <FieldLabel className="mb-1.5 block">{t("ApplicationApi.AllowedIps")}</FieldLabel>
            <Textarea
              rows={4}
              className="font-mono text-xs"
              placeholder={"203.0.113.10\n10.0.0.0/24\n2001:db8::/32"}
              spellCheck={false}
              aria-invalid={errors.allowedIps ? "true" : undefined}
              {...register("allowedIps")}
            />
            <FieldDescription>{t("ApplicationApi.Form.AllowedIpsHint", MAX_IPS)}</FieldDescription>
            <FieldError errors={[errors.allowedIps]} />
          </Field>

          <DialogFooter className="mx-0 mb-0 p-0">
            <Button type="button" variant="outline" onClick={onClose}>{t("Common.Cancel")}</Button>
            <Button type="submit" disabled={busy}>{applicationKey ? t("Common.SaveChanges") : t("ApplicationApi.Create")}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
