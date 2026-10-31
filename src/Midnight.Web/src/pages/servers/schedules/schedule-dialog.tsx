import { schedulesApi, serverFeatureKeys, type Schedule } from "@/api/server-features";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import { notifyError, notifySuccess } from "@/lib/notify";
import { cn } from "@/utils";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import * as z from "zod";
import { useServerContext } from "../hooks";
import { applyProblemErrors } from "../schema";
import { cronFieldPattern, cronPresets, describeCron, sameCron } from "./cron";

const cronFields = ["minute", "hour", "dayOfMonth", "month", "dayOfWeek"] as const;

const cronLabels: Record<(typeof cronFields)[number], string> = {
  minute: "ServerSchedules.Minute",
  hour: "ServerSchedules.Hour",
  dayOfMonth: "ServerSchedules.DayOfMonth",
  month: "ServerSchedules.Month",
  dayOfWeek: "ServerSchedules.DayOfWeek",
};

/** Create (schedule = null) or edit a schedule */
export function ScheduleDialog({ open, schedule, onClose, onSaved }: {
  open: boolean;
  schedule: Schedule | null;
  onClose: () => void;
  onSaved?: (schedule: Schedule) => void;
}) {
  const { server } = useServerContext();
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const schema = useMemo(() => {
    const field = z.string().trim().min(1, t("Validation.Required")).max(64).regex(cronFieldPattern, t("ServerSchedules.CronInvalid"));

    return z.object({
      name: z.string().trim().min(1, t("Validation.Required")).max(191, t("Servers.Validation.MaxLength", 191)),
      minute: field,
      hour: field,
      dayOfMonth: field,
      month: field,
      dayOfWeek: field,
      isActive: z.boolean(),
      onlyWhenOnline: z.boolean(),
    });
  }, [t]);
  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    values: schedule
      ? {
          name: schedule.name,
          minute: schedule.minute,
          hour: schedule.hour,
          dayOfMonth: schedule.dayOfMonth,
          month: schedule.month,
          dayOfWeek: schedule.dayOfWeek,
          isActive: schedule.isActive,
          onlyWhenOnline: schedule.onlyWhenOnline,
        }
      : { name: "", ...cronPresets[4]!.parts, isActive: true, onlyWhenOnline: false },
  });
  const { register, control, setValue, formState: { errors } } = form;
  const watched = useWatch({ control });
  const parts = {
    minute: watched.minute ?? "*",
    hour: watched.hour ?? "*",
    dayOfMonth: watched.dayOfMonth ?? "*",
    month: watched.month ?? "*",
    dayOfWeek: watched.dayOfWeek ?? "*",
  };
  const valid = cronFields.every((field) => cronFieldPattern.test(parts[field].trim()));

  const save = async (values: Values) => {
    setBusy(true);

    try {
      const request = { ...values, name: values.name.trim() };
      const saved = schedule ? await schedulesApi.update(server.uuid, schedule.id, request) : await schedulesApi.create(server.uuid, request);
      await queryClient.invalidateQueries({ queryKey: serverFeatureKeys.schedules(server.uuid) });
      notifySuccess(schedule ? t("Common.Saved") : t("ServerSchedules.Created", saved.name));
      onSaved?.(saved);
      onClose();
    } catch (error) {
      applyProblemErrors(form.setError, error, {
        Name: "name",
        Minute: "minute",
        Hour: "hour",
        DayOfMonth: "dayOfMonth",
        Month: "month",
        DayOfWeek: "dayOfWeek",
      });
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(value) => !value && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{schedule ? t("ServerSchedules.Edit") : t("ServerSchedules.Create")}</DialogTitle>
          <DialogDescription>{t("ServerSchedules.DialogHint")}</DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(save)} noValidate className="space-y-5">
          <Field data-invalid={Boolean(errors.name)}>
            <FieldLabel className="mb-1.5 block">{t("Common.Name")}</FieldLabel>
            <Input placeholder={t("ServerSchedules.NamePlaceholder")} aria-invalid={errors.name ? "true" : undefined} {...register("name")} />
            <FieldError errors={[errors.name]} />
          </Field>

          <div>
            <p className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">{t("ServerSchedules.Presets")}</p>
            <div className="flex flex-wrap gap-2">
              {cronPresets.map((preset) => {
                const active = sameCron(parts, preset.parts);

                return (
                  <button
                    key={preset.key}
                    type="button"
                    onClick={() => {
                      for (const field of cronFields) setValue(field, preset.parts[field], { shouldDirty: true, shouldValidate: true });
                    }}
                    className={cn(
                      "rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors",
                      active
                        ? "border-brand-500 bg-brand-500/10 text-brand-600 dark:text-brand-400"
                        : "border-gray-200 text-gray-600 hover:border-gray-300 hover:text-gray-800 dark:border-gray-800 dark:text-gray-400 dark:hover:border-gray-700 dark:hover:text-white",
                    )}
                  >
                    {t(`ServerSchedules.Preset.${preset.key}`)}
                  </button>
                );
              })}
            </div>
          </div>

          <div className="rounded-xl border border-gray-200 p-4 dark:border-gray-800">
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
              {cronFields.map((field) => (
                <Field key={field} data-invalid={Boolean(errors[field])}>
                  <FieldLabel className="mb-1.5 block text-xs">{t(cronLabels[field])}</FieldLabel>
                  <Input className="h-10 text-center font-mono" autoComplete="off" spellCheck={false} aria-invalid={errors[field] ? "true" : undefined} {...register(field)} />
                  <FieldError errors={[errors[field]]} />
                </Field>
              ))}
            </div>
            <p className="mt-3 text-sm text-gray-600 dark:text-gray-300">
              {valid ? describeCron(parts, t) : <span className="text-red-500">{t("ServerSchedules.CronInvalid")}</span>}
            </p>
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("ServerSchedules.CronHint")}</p>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <Controller
              control={control}
              name="isActive"
              render={({ field }) => (
                <label className="flex cursor-pointer items-start justify-between gap-4 rounded-xl border border-gray-200 p-4 dark:border-gray-800">
                  <span>
                    <span className="block text-sm font-medium text-gray-800 dark:text-white/90">{t("ServerSchedules.Active")}</span>
                    <span className="mt-0.5 block text-xs text-gray-500 dark:text-gray-400">{t("ServerSchedules.ActiveHint")}</span>
                  </span>
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </label>
              )}
            />
            <Controller
              control={control}
              name="onlyWhenOnline"
              render={({ field }) => (
                <label className="flex cursor-pointer items-start justify-between gap-4 rounded-xl border border-gray-200 p-4 dark:border-gray-800">
                  <span>
                    <span className="block text-sm font-medium text-gray-800 dark:text-white/90">{t("ServerSchedules.OnlyWhenOnline")}</span>
                    <span className="mt-0.5 block text-xs text-gray-500 dark:text-gray-400">{t("ServerSchedules.OnlyWhenOnlineHint")}</span>
                  </span>
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </label>
              )}
            />
          </div>

          <DialogFooter className="mx-0 mb-0 p-0">
            <Button type="button" variant="outline" onClick={onClose}>{t("Common.Cancel")}</Button>
            <Button type="submit" disabled={busy}>{schedule ? t("Common.SaveChanges") : t("ServerSchedules.Create")}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
