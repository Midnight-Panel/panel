import { adminServersApi } from "@/api/servers";
import { SectionCard } from "@/components/shared/section-card";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import { FloppyDiscIcon, PencilIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import * as z from "zod";
import { applyProblemErrors } from "@/pages/servers/schema";
import { UserPicker, type UserOption } from "./components/user-picker";
import { adminServerQueryKey, useAdminServer } from "./hooks";

export default function AdminServerDetailsPage() {
  const server = useAdminServer();
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const schema = useMemo(
    () =>
      z.object({
        name: z.string().trim().min(1, t("Validation.Required")).max(64, t("Servers.Validation.MaxLength", 64)),
        description: z.string().max(512, t("Servers.Validation.MaxLength", 512)),
        owner: z.custom<UserOption | null>().refine((owner) => Boolean(owner), t("Validation.Required")),
      }),
    [t],
  );

  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({
    mode: "onTouched",
    resolver: zodResolver(schema),
    defaultValues: {
      name: server.name,
      description: server.description ?? "",
      owner: { id: server.ownerId, username: server.ownerUsername, email: server.ownerEmail },
    },
  });
  const { register, control, formState: { errors, isDirty } } = form;

  const save = async (values: Values) => {
    if (!values.owner) return;
    setBusy(true);

    try {
      const updated = await adminServersApi.updateDetails(server.id, {
        name: values.name.trim(),
        description: values.description.trim() || null,
        ownerId: values.owner.id,
      });
      queryClient.setQueryData(adminServerQueryKey(server.id), updated);
      await queryClient.invalidateQueries({ queryKey: ["admin", "servers", "list"] });
      form.reset(values);
      notifySuccess(t("Common.Saved"));
    } catch (error) {
      applyProblemErrors(form.setError, error, { Name: "name", Description: "description", OwnerId: "owner" });
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={form.handleSubmit(save)} noValidate className="max-w-3xl">
      <SectionCard icon={<PencilIcon />} title={t("Servers.Sections.Details")} description={t("Servers.DetailsTabHint")}>
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <Field data-invalid={Boolean(errors.name)}>
            <FieldLabel className="mb-1.5 block">{t("Common.Name")}</FieldLabel>
            <Input aria-invalid={errors.name ? "true" : undefined} {...register("name")} />
            <FieldError errors={[errors.name]} />
          </Field>
          <Field data-invalid={Boolean(errors.owner)}>
            <FieldLabel className="mb-1.5 block">{t("Servers.Owner")}</FieldLabel>
            <Controller
              control={control}
              name="owner"
              render={({ field }) => <UserPicker value={field.value} onChange={(user) => { field.onChange(user); field.onBlur(); }} invalid={Boolean(errors.owner)} />}
            />
            <FieldDescription className="text-xs">{t("Servers.OwnerHint")}</FieldDescription>
            <FieldError>{errors.owner?.message}</FieldError>
          </Field>
        </div>
        <Field data-invalid={Boolean(errors.description)}>
          <FieldLabel className="mb-1.5 block">{t("Common.Description")}</FieldLabel>
          <Textarea rows={3} aria-invalid={errors.description ? "true" : undefined} {...register("description")} />
          <FieldError errors={[errors.description]} />
        </Field>
        <div className="flex justify-end">
          <Button type="submit" disabled={busy || !isDirty}>
            <FloppyDiscIcon />
            {t("Common.SaveChanges")}
          </Button>
        </div>
      </SectionCard>
    </form>
  );
}
