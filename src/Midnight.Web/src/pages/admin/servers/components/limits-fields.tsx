import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { InputGroup, InputGroupAddon, InputGroupInput, InputGroupText } from "@/components/ui/input-group";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import { formatCpu, mibHint } from "@/lib/servers";
import type { featureLimitsSchema, limitsSchema } from "@/pages/servers/schema";
import type { ReactNode } from "react";
import { useWatch, type FieldValues, type Path, type PathValue, type UseFormRegisterReturn, type UseFormReturn } from "react-hook-form";
import type * as z from "zod";

export type LimitsValues = z.infer<ReturnType<typeof limitsSchema>>;
export type FeatureLimitsValues = z.infer<ReturnType<typeof featureLimitsSchema>>;

interface LimitsShape {
  limits: LimitsValues;
  featureLimits: FeatureLimitsValues;
}

function NumberField({ label, unit, hint, error, input }: {
  label: string;
  unit: string;
  hint?: ReactNode;
  error?: string;
  input: UseFormRegisterReturn;
}) {
  return (
    <Field data-invalid={Boolean(error)}>
      <FieldLabel className="mb-1.5 block">{label}</FieldLabel>
      <InputGroup>
        <InputGroupInput type="number" aria-invalid={error ? "true" : undefined} {...input} />
        <InputGroupAddon align="inline-end">
          <InputGroupText className="text-xs text-gray-500 dark:text-gray-400">{unit}</InputGroupText>
        </InputGroupAddon>
      </InputGroup>
      {hint && !error && <FieldDescription className="text-xs">{hint}</FieldDescription>}
      <FieldError>{error}</FieldError>
    </Field>
  );
}

function useBinding<T extends LimitsShape & FieldValues>(form: UseFormReturn<T>) {
  const path = (name: string) => name as Path<T>;
  const error = (name: string) => form.getFieldState(path(name), form.formState).error?.message;
  const number = (name: string): UseFormRegisterReturn => form.register(path(name), { valueAsNumber: true });

  return { path, error, number };
}

/** CPU, memory, swap, disk, io weight, threads and OOM killer (ServerLimitsDto) */
export function ResourceFields<T extends LimitsShape & FieldValues>({ form }: { form: UseFormReturn<T> }) {
  const { t } = useT();
  const { path, error, number } = useBinding(form);
  const limits = useWatch({ control: form.control, name: path("limits") }) as LimitsValues;

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
        <NumberField
          label={t("Servers.Cpu")}
          unit="%"
          input={number("limits.cpu")}
          error={error("limits.cpu")}
          hint={Number.isFinite(limits.cpu) ? `${t("Servers.CpuHint")} ${limits.cpu > 0 ? `= ${formatCpu(limits.cpu, t)}` : `· ${t("Servers.ZeroUnlimited")}`}` : t("Servers.CpuHint")}
        />
        <NumberField
          label={t("Servers.Memory")}
          unit="MiB"
          input={number("limits.memory")}
          error={error("limits.memory")}
          hint={mibHint(limits.memory, t)}
        />
        <NumberField
          label={t("Servers.Swap")}
          unit="MiB"
          input={number("limits.swap")}
          error={error("limits.swap")}
          hint={`${t("Servers.SwapHint")}${limits.swap > 0 ? ` · ${mibHint(limits.swap, t)}` : ""}`}
        />
        <NumberField
          label={t("Servers.Disk")}
          unit="MiB"
          input={number("limits.disk")}
          error={error("limits.disk")}
          hint={mibHint(limits.disk, t, t("Servers.ZeroUnlimited"))}
        />
        <NumberField
          label={t("Servers.IoWeight")}
          unit="10–1000"
          input={number("limits.ioWeight")}
          error={error("limits.ioWeight")}
          hint={t("Servers.IoWeightHint")}
        />
        <Field data-invalid={Boolean(error("limits.threads"))}>
          <FieldLabel className="mb-1.5 block">{t("Servers.Threads")}</FieldLabel>
          <InputGroup>
            <InputGroupInput placeholder="0-3" className="font-mono" aria-invalid={error("limits.threads") ? "true" : undefined} {...form.register(path("limits.threads"))} />
          </InputGroup>
          {!error("limits.threads") && <FieldDescription className="text-xs">{t("Servers.ThreadsHint")}</FieldDescription>}
          <FieldError>{error("limits.threads")}</FieldError>
        </Field>
      </div>

      <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-gray-200 p-4 dark:border-gray-800">
        <Switch
          checked={limits.oomKillerDisabled}
          onCheckedChange={(checked) =>
            form.setValue(path("limits.oomKillerDisabled"), Boolean(checked) as PathValue<T, Path<T>>, { shouldDirty: true })
          }
        />
        <span>
          <span className="block text-sm font-medium text-gray-700 dark:text-gray-300">{t("Servers.OomKillerDisabled")}</span>
          <span className="block text-xs text-gray-500 dark:text-gray-400">{t("Servers.OomKillerDisabledHint")}</span>
        </span>
      </label>
    </div>
  );
}

/** Databases, allocations and backups the owner may create (ServerFeatureLimitsDto) */
export function FeatureLimitFields<T extends LimitsShape & FieldValues>({ form }: { form: UseFormReturn<T> }) {
  const { t } = useT();
  const { error, number } = useBinding(form);

  return (
    <div className="grid grid-cols-1 gap-5 sm:grid-cols-3">
      <NumberField label={t("Servers.Databases")} unit="0–1000" input={number("featureLimits.databases")} error={error("featureLimits.databases")} />
      <NumberField label={t("Servers.ExtraAllocations")} unit="0–1000" input={number("featureLimits.allocations")} error={error("featureLimits.allocations")} />
      <NumberField label={t("Servers.Backups")} unit="0–1000" input={number("featureLimits.backups")} error={error("featureLimits.backups")} />
    </div>
  );
}
