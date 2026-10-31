import { schedulesApi, serverFeatureKeys, type ScheduleTask, type ScheduleTaskAction } from "@/api/server-features";
import type { PowerAction } from "@/api/types";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import { notifyError, notifySuccess } from "@/lib/notify";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import * as z from "zod";
import { useServerContext } from "../hooks";
import { applyProblemErrors } from "../schema";

const taskActions: ScheduleTaskAction[] = ["Command", "Power", "Backup"];
const powerActions: PowerAction[] = ["Start", "Stop", "Restart", "Kill"];

/** Create (task = null) or edit a task of a schedule */
export function TaskDialog({ scheduleId, task, isFirst, onClose }: {
  scheduleId: number;
  task: ScheduleTask | null;
  /** The first task has no "previous task" to wait for */
  isFirst: boolean;
  onClose: () => void;
}) {
  const { server } = useServerContext();
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const schema = useMemo(
    () =>
      z
        .object({
          action: z.enum(["Command", "Power", "Backup"]),
          payload: z.string().max(2048, t("Servers.Validation.MaxLength", 2048)),
          timeOffset: z.coerce.number().int(t("ServerSchedules.OffsetRange")).min(0, t("ServerSchedules.OffsetRange")).max(900, t("ServerSchedules.OffsetRange")),
          continueOnFailure: z.boolean(),
        })
        .superRefine((value, context) => {
          if (value.action !== "Backup" && !value.payload.trim())
            context.addIssue({ code: z.ZodIssueCode.custom, path: ["payload"], message: t("Validation.Required") });
        }),
    [t],
  );
  type FormInput = z.input<typeof schema>;
  type Values = z.output<typeof schema>;

  const form = useForm<FormInput, unknown, Values>({
    resolver: zodResolver(schema),
    defaultValues: task
      ? { action: task.action, payload: task.payload, timeOffset: task.timeOffset, continueOnFailure: task.continueOnFailure }
      : { action: "Command", payload: "", timeOffset: 0, continueOnFailure: false },
  });
  const { register, control, setValue, formState: { errors } } = form;
  const action = useWatch({ control, name: "action" });

  const save = async (values: Values) => {
    setBusy(true);

    try {
      const request = { ...values, payload: values.action === "Command" ? values.payload.trim() : values.payload };

      if (task) await schedulesApi.updateTask(server.uuid, scheduleId, task.id, request);
      else await schedulesApi.createTask(server.uuid, scheduleId, request);

      await queryClient.invalidateQueries({ queryKey: serverFeatureKeys.schedules(server.uuid) });
      notifySuccess(task ? t("Common.Saved") : t("ServerSchedules.TaskCreated"));
      onClose();
    } catch (error) {
      applyProblemErrors(form.setError, error, { Payload: "payload", TimeOffset: "timeOffset", Action: "action" });
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{task ? t("ServerSchedules.EditTask") : t("ServerSchedules.AddTask")}</DialogTitle>
          <DialogDescription>{t("ServerSchedules.TaskHint")}</DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(save)} noValidate className="space-y-5">
          <Field>
            <FieldLabel className="mb-1.5 block">{t("ServerSchedules.Action")}</FieldLabel>
            <Controller
              control={control}
              name="action"
              render={({ field }) => (
                <Select<ScheduleTaskAction>
                  value={field.value}
                  items={taskActions.map((value) => ({ value, label: t(`ServerSchedules.Actions.${value}`) }))}
                  onValueChange={(value) => {
                    if (value === null) return;
                    field.onChange(value);
                    setValue("payload", value === "Power" ? "Restart" : "", { shouldValidate: false });
                  }}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {taskActions.map((value) => (
                      <SelectItem key={value} value={value}>{t(`ServerSchedules.Actions.${value}`)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </Field>

          {action === "Command" && (
            <Field data-invalid={Boolean(errors.payload)}>
              <FieldLabel className="mb-1.5 block">{t("ServerSchedules.Command")}</FieldLabel>
              <Input className="font-mono" autoComplete="off" spellCheck={false} placeholder="say Restarting in 5 minutes" aria-invalid={errors.payload ? "true" : undefined} {...register("payload")} />
              <FieldError errors={[errors.payload]} />
            </Field>
          )}

          {action === "Power" && (
            <Field data-invalid={Boolean(errors.payload)}>
              <FieldLabel className="mb-1.5 block">{t("ServerSchedules.PowerAction")}</FieldLabel>
              <Controller
                control={control}
                name="payload"
                render={({ field }) => (
                  <Select<string>
                    value={field.value || null}
                    items={powerActions.map((value) => ({ value, label: t(`Servers.Power.${value}`) }))}
                    onValueChange={(value) => value !== null && field.onChange(value)}
                  >
                    <SelectTrigger aria-invalid={errors.payload ? "true" : undefined}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {powerActions.map((value) => (
                        <SelectItem key={value} value={value}>{t(`Servers.Power.${value}`)}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              <FieldError errors={[errors.payload]} />
            </Field>
          )}

          {action === "Backup" && (
            <Field data-invalid={Boolean(errors.payload)}>
              <FieldLabel className="mb-1.5 block">{t("ServerBackups.IgnoredFiles")}</FieldLabel>
              <Textarea rows={4} spellCheck={false} className="font-mono text-xs" placeholder={"logs/\n*.log"} {...register("payload")} />
              <FieldDescription>{t("ServerSchedules.BackupTaskHint")}</FieldDescription>
              <FieldError errors={[errors.payload]} />
            </Field>
          )}

          <Field data-invalid={Boolean(errors.timeOffset)}>
            <FieldLabel className="mb-1.5 block">{t("ServerSchedules.TimeOffset")}</FieldLabel>
            <Input type="number" min={0} max={900} aria-invalid={errors.timeOffset ? "true" : undefined} {...register("timeOffset")} />
            <FieldDescription>{isFirst ? t("ServerSchedules.TimeOffsetFirstHint") : t("ServerSchedules.TimeOffsetHint")}</FieldDescription>
            <FieldError errors={[errors.timeOffset]} />
          </Field>

          <Controller
            control={control}
            name="continueOnFailure"
            render={({ field }) => (
              <label className="flex cursor-pointer items-start justify-between gap-4 rounded-xl border border-gray-200 p-4 dark:border-gray-800">
                <span>
                  <span className="block text-sm font-medium text-gray-800 dark:text-white/90">{t("ServerSchedules.ContinueOnFailure")}</span>
                  <span className="mt-0.5 block text-xs text-gray-500 dark:text-gray-400">{t("ServerSchedules.ContinueOnFailureHint")}</span>
                </span>
                <Switch checked={field.value} onCheckedChange={field.onChange} />
              </label>
            )}
          />

          <DialogFooter className="mx-0 mb-0 p-0">
            <Button type="button" variant="outline" onClick={onClose}>{t("Common.Cancel")}</Button>
            <Button type="submit" disabled={busy}>{task ? t("Common.SaveChanges") : t("ServerSchedules.AddTask")}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
