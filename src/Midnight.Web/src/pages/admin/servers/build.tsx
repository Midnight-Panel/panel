import { adminServersApi } from "@/api/servers";
import { SectionCard } from "@/components/shared/section-card";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { useT } from "@/i18n";
import { ChipIcon, FloppyDiscIcon, GlobeIcon, LayerIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import type { AllocationOption } from "@/lib/servers";
import { applyProblemErrors, featureLimitsSchema, limitsErrorPaths, limitsSchema } from "@/pages/servers/schema";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import * as z from "zod";
import { AllocationMultiSelect, AllocationSelect } from "./components/allocation-picker";
import { FeatureLimitFields, ResourceFields } from "./components/limits-fields";
import { adminServerQueryKey, useAdminServer } from "./hooks";

export default function AdminServerBuildPage() {
  const server = useAdminServer();
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const schema = useMemo(
    () =>
      z.object({
        limits: limitsSchema(t),
        featureLimits: featureLimitsSchema(t),
        primaryAllocation: z.custom<AllocationOption | null>().refine((allocation) => Boolean(allocation), t("Servers.Validation.PrimaryAllocation")),
        additionalAllocations: z.custom<AllocationOption[]>(),
      }),
    [t],
  );

  type Values = z.infer<typeof schema>;

  const own: AllocationOption[] = server.allocations.map(({ id, ipAddress, port, notes }) => ({ id, ipAddress, port, notes }));
  const primaryAllocation = server.allocations.find((allocation) => allocation.isPrimary);

  const form = useForm<Values>({
    mode: "onTouched",
    resolver: zodResolver(schema),
    defaultValues: {
      limits: { ...server.limits, threads: server.limits.threads ?? "" },
      featureLimits: server.featureLimits,
      primaryAllocation: own.find((allocation) => allocation.id === primaryAllocation?.id) ?? null,
      additionalAllocations: own.filter((allocation) => allocation.id !== primaryAllocation?.id),
    },
  });
  const { setValue, control, formState: { errors, isDirty, isSubmitted } } = form;
  const [primary, additional] = useWatch({ control, name: ["primaryAllocation", "additionalAllocations"] });

  const save = async (values: Values) => {
    if (!values.primaryAllocation) return;
    setBusy(true);

    try {
      const updated = await adminServersApi.updateBuild(server.id, {
        limits: { ...values.limits, threads: values.limits.threads.trim() || null },
        featureLimits: values.featureLimits,
        primaryAllocationId: values.primaryAllocation.id,
        additionalAllocationIds: values.additionalAllocations.map((allocation) => allocation.id),
      });
      queryClient.setQueryData(adminServerQueryKey(server.id), updated);
      await queryClient.invalidateQueries({ queryKey: ["admin", "servers", "list"] });
      await queryClient.invalidateQueries({ queryKey: ["admin", "nodes", server.nodeId, "allocations"] });
      form.reset(values);
      notifySuccess(t("Servers.BuildSaved"));
    } catch (error) {
      applyProblemErrors(form.setError, error, { ...limitsErrorPaths, PrimaryAllocationId: "primaryAllocation" });
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={form.handleSubmit(save)} noValidate className="space-y-6">
      <SectionCard icon={<ChipIcon />} title={t("Servers.Sections.Resources")} description={t("Servers.BuildHint")}>
        <ResourceFields form={form} />
      </SectionCard>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <SectionCard icon={<GlobeIcon />} title={t("Nodes.Allocations")} description={t("Servers.AllocationsHint", server.nodeName)}>
          <Field data-invalid={Boolean(errors.primaryAllocation)}>
            <FieldLabel className="mb-1.5 block">{t("Servers.PrimaryAllocation")}</FieldLabel>
            <AllocationSelect
              nodeId={server.nodeId}
              value={primary}
              extra={own}
              exclude={additional.map((allocation) => allocation.id)}
              invalid={Boolean(errors.primaryAllocation)}
              onChange={(allocation) => setValue("primaryAllocation", allocation, { shouldDirty: true, shouldValidate: isSubmitted })}
            />
            <FieldError>{errors.primaryAllocation?.message}</FieldError>
          </Field>
          <Field>
            <FieldLabel className="mb-1.5 block">{t("Servers.AdditionalAllocations")}</FieldLabel>
            <AllocationMultiSelect
              nodeId={server.nodeId}
              value={additional}
              extra={own}
              exclude={primary ? [primary.id] : []}
              onChange={(allocations) => setValue("additionalAllocations", allocations, { shouldDirty: true })}
            />
            <FieldDescription className="text-xs">{t("Servers.AdditionalAllocationsHint")}</FieldDescription>
          </Field>
        </SectionCard>

        <SectionCard icon={<LayerIcon />} title={t("Servers.Sections.FeatureLimits")} description={t("Servers.Sections.FeatureLimitsHint")}>
          <FeatureLimitFields form={form} />
        </SectionCard>
      </div>

      <div className="flex justify-end">
        <Button type="submit" disabled={busy || !isDirty}>
          <FloppyDiscIcon />
          {t("Common.SaveChanges")}
        </Button>
      </div>
    </form>
  );
}
