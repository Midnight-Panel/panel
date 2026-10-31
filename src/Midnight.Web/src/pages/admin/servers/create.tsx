import { adminServersApi } from "@/api/servers";
import { shardsApi } from "@/api/shards";
import type { CreateServerRequest } from "@/api/types";
import { RequirePermission } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { DefinitionList, SectionCard } from "@/components/shared/section-card";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import { BoxIcon, ChipIcon, GearIcon, GlobeIcon, LayerIcon, PlusIcon, UserIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { allocationLabel, defaultFeatureLimits, defaultLimits, formatCpu, formatMib, type AllocationOption } from "@/lib/servers";
import {
  applyProblemErrors,
  featureLimitsSchema,
  limitsErrorPaths,
  limitsSchema,
  variableErrorPaths,
  variablesFromShard,
  variablesSchema,
  variablesToRecord,
} from "@/pages/servers/schema";
import { VariableFields } from "@/pages/servers/components/variable-fields";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type ReactNode } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { useNavigate } from "react-router";
import * as z from "zod";
import { AllocationMultiSelect, AllocationSelect } from "./components/allocation-picker";
import { FeatureLimitFields, ResourceFields } from "./components/limits-fields";
import { DockerImageSelect, NodeSelect, ShardSelect } from "./components/selects";
import { UserPicker, type UserOption } from "./components/user-picker";
import { useShardDetail } from "./hooks";

type Translate = (key: string, ...args: Array<string | number>) => string;

function createSchema(t: Translate) {
  const required = t("Validation.Required");

  return z.object({
    name: z.string().trim().min(1, required).max(64, t("Servers.Validation.MaxLength", 64)),
    description: z.string().max(512, t("Servers.Validation.MaxLength", 512)),
    owner: z.custom<UserOption | null>().refine((owner) => Boolean(owner), required),
    nodeId: z.number().nullable().refine((id) => Boolean(id), required),
    primaryAllocation: z.custom<AllocationOption | null>().refine((allocation) => Boolean(allocation), t("Servers.Validation.PrimaryAllocation")),
    additionalAllocations: z.custom<AllocationOption[]>(),
    shardId: z.number().nullable().refine((id) => Boolean(id), required),
    dockerImage: z.string().max(255),
    startupCommand: z.string().max(4096, t("Servers.Validation.MaxLength", 4096)),
    limits: limitsSchema(t),
    featureLimits: featureLimitsSchema(t),
    variables: variablesSchema(t),
    startOnCompletion: z.boolean(),
    skipInstall: z.boolean(),
  });
}

type CreateValues = z.infer<ReturnType<typeof createSchema>>;

const defaults: CreateValues = {
  name: "",
  description: "",
  owner: null,
  nodeId: null,
  primaryAllocation: null,
  additionalAllocations: [],
  shardId: null,
  dockerImage: "",
  startupCommand: "",
  limits: { ...defaultLimits, threads: "" },
  featureLimits: { ...defaultFeatureLimits },
  variables: [],
  startOnCompletion: true,
  skipInstall: false,
};

function OptionToggle({ checked, onChange, title, description }: { checked: boolean; onChange: (checked: boolean) => void; title: string; description: string }) {
  return (
    <label className="flex cursor-pointer items-start gap-3">
      <Switch checked={checked} onCheckedChange={(value) => onChange(Boolean(value))} />
      <span>
        <span className="block text-sm font-medium text-gray-700 dark:text-gray-300">{title}</span>
        <span className="block text-xs text-gray-500 dark:text-gray-400">{description}</span>
      </span>
    </label>
  );
}

function CreateServerForm() {
  const { t } = useT();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);
  const schema = useMemo(() => createSchema(t), [t]);

  const form = useForm<CreateValues>({ mode: "onTouched", resolver: zodResolver(schema), defaultValues: defaults });
  const { register, control, setValue, formState: { errors, isSubmitted } } = form;

  const [nodeId, shardId, primary, additional, variables, limits, name, owner, dockerImage] = useWatch({
    control,
    name: ["nodeId", "shardId", "primaryAllocation", "additionalAllocations", "variables", "limits", "name", "owner", "dockerImage"],
  });

  const shard = useShardDetail(shardId);

  const selectShard = async (id: number) => {
    setValue("shardId", id, { shouldDirty: true, shouldValidate: isSubmitted });

    try {
      const detail = await queryClient.fetchQuery({ queryKey: ["admin", "shards", id], queryFn: () => shardsApi.getShard(id) });
      setValue("dockerImage", detail.defaultDockerImage || Object.values(detail.dockerImages)[0] || "", { shouldDirty: true });
      setValue("startupCommand", "");
      setValue("variables", variablesFromShard(detail.variables), { shouldDirty: true });
    } catch (error) {
      notifyError(error);
    }
  };

  const submit = async (values: CreateValues) => {
    if (!values.owner || values.nodeId === null || values.shardId === null || !values.primaryAllocation) return;

    const request: CreateServerRequest = {
      name: values.name.trim(),
      description: values.description.trim() || null,
      ownerId: values.owner.id,
      nodeId: values.nodeId,
      shardId: values.shardId,
      dockerImage: values.dockerImage || null,
      startupCommand: values.startupCommand.trim() || null,
      limits: { ...values.limits, threads: values.limits.threads.trim() || null },
      featureLimits: values.featureLimits,
      primaryAllocationId: values.primaryAllocation.id,
      additionalAllocationIds: values.additionalAllocations.map((allocation) => allocation.id),
      variables: variablesToRecord(values.variables),
      startOnCompletion: values.startOnCompletion,
      skipInstall: values.skipInstall,
    };

    setBusy(true);

    try {
      const server = await adminServersApi.create(request);
      await queryClient.invalidateQueries({ queryKey: ["admin", "servers"] });
      await queryClient.invalidateQueries({ queryKey: ["admin", "nodes"] });
      notifySuccess(t("Servers.Created", server.name));
      navigate(`/admin/servers/${server.id}`);
    } catch (error) {
      applyProblemErrors(form.setError, error, {
        ...variableErrorPaths(values.variables),
        ...limitsErrorPaths,
        Name: "name",
        Description: "description",
        OwnerId: "owner",
        NodeId: "nodeId",
        ShardId: "shardId",
        DockerImage: "dockerImage",
        StartupCommand: "startupCommand",
        PrimaryAllocationId: "primaryAllocation",
      });
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const summary: Array<[string, ReactNode]> = [
    [t("Common.Name"), name.trim() || "-"],
    [t("Servers.Owner"), owner?.username ?? "-"],
    [t("Servers.Shard"), shard.data?.name ?? "-"],
    [t("Servers.PrimaryAllocation"), primary ? <span className="font-mono">{allocationLabel(primary)}</span> : "-"],
    [t("Servers.Cpu"), Number.isFinite(limits.cpu) ? formatCpu(limits.cpu, t) : "-"],
    [t("Servers.Memory"), Number.isFinite(limits.memory) ? formatMib(limits.memory) : "-"],
    [t("Servers.Disk"), Number.isFinite(limits.disk) ? (limits.disk > 0 ? formatMib(limits.disk) : t("Servers.Unlimited")) : "-"],
  ];

  return (
    <form onSubmit={form.handleSubmit(submit)} noValidate className="grid grid-cols-1 items-start gap-6 xl:grid-cols-3">
      <div className="space-y-6 xl:col-span-2">
        <SectionCard icon={<UserIcon />} title={t("Servers.Sections.Details")} description={t("Servers.Sections.DetailsHint")}>
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <Field data-invalid={Boolean(errors.name)}>
              <FieldLabel className="mb-1.5 block">{t("Common.Name")}</FieldLabel>
              <Input placeholder="Survival" aria-invalid={errors.name ? "true" : undefined} {...register("name")} />
              <FieldError errors={[errors.name]} />
            </Field>
            <Field data-invalid={Boolean(errors.owner)}>
              <FieldLabel className="mb-1.5 block">{t("Servers.Owner")}</FieldLabel>
              <Controller
                control={control}
                name="owner"
                render={({ field }) => <UserPicker value={field.value} onChange={(user) => { field.onChange(user); field.onBlur(); }} invalid={Boolean(errors.owner)} />}
              />
              <FieldError>{errors.owner?.message}</FieldError>
            </Field>
          </div>
          <Field data-invalid={Boolean(errors.description)}>
            <FieldLabel className="mb-1.5 block">{t("Common.Description")}</FieldLabel>
            <Textarea rows={3} placeholder={t("Servers.DescriptionPlaceholder")} aria-invalid={errors.description ? "true" : undefined} {...register("description")} />
            <FieldError errors={[errors.description]} />
          </Field>
        </SectionCard>

        <SectionCard icon={<GlobeIcon />} title={t("Servers.Sections.Network")} description={t("Servers.Sections.NetworkHint")}>
          <Field data-invalid={Boolean(errors.nodeId)}>
            <FieldLabel className="mb-1.5 block">{t("Servers.Node")}</FieldLabel>
            <NodeSelect
              value={nodeId}
              invalid={Boolean(errors.nodeId)}
              onChange={(id) => {
                setValue("nodeId", id, { shouldDirty: true, shouldValidate: isSubmitted });
                setValue("primaryAllocation", null);
                setValue("additionalAllocations", []);
              }}
            />
            <FieldError>{errors.nodeId?.message}</FieldError>
          </Field>
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <Field data-invalid={Boolean(errors.primaryAllocation)}>
              <FieldLabel className="mb-1.5 block">{t("Servers.PrimaryAllocation")}</FieldLabel>
              <AllocationSelect
                nodeId={nodeId}
                value={primary}
                exclude={additional.map((allocation) => allocation.id)}
                invalid={Boolean(errors.primaryAllocation)}
                onChange={(allocation) => setValue("primaryAllocation", allocation, { shouldDirty: true, shouldValidate: isSubmitted })}
              />
              <FieldDescription className="text-xs">{t("Servers.PrimaryAllocationHint")}</FieldDescription>
              <FieldError>{errors.primaryAllocation?.message}</FieldError>
            </Field>
            <Field>
              <FieldLabel className="mb-1.5 block">{t("Servers.AdditionalAllocations")}</FieldLabel>
              <AllocationMultiSelect
                nodeId={nodeId}
                value={additional}
                exclude={primary ? [primary.id] : []}
                onChange={(allocations) => setValue("additionalAllocations", allocations, { shouldDirty: true })}
              />
              <FieldDescription className="text-xs">{t("Servers.AdditionalAllocationsHint")}</FieldDescription>
            </Field>
          </div>
        </SectionCard>

        <SectionCard icon={<BoxIcon />} title={t("Servers.Sections.Shard")} description={t("Servers.Sections.ShardHint")}>
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <Field data-invalid={Boolean(errors.shardId)}>
              <FieldLabel className="mb-1.5 block">{t("Servers.Shard")}</FieldLabel>
              <ShardSelect value={shardId} invalid={Boolean(errors.shardId)} onChange={(id) => void selectShard(id)} />
              {shard.data?.description && <FieldDescription className="text-xs">{shard.data.description}</FieldDescription>}
              <FieldError>{errors.shardId?.message}</FieldError>
            </Field>
            <Field>
              <FieldLabel className="mb-1.5 block">{t("Servers.DockerImage")}</FieldLabel>
              <DockerImageSelect
                images={shard.data?.dockerImages ?? {}}
                value={dockerImage}
                disabled={!shard.data}
                onChange={(image) => setValue("dockerImage", image, { shouldDirty: true })}
              />
            </Field>
          </div>
          <Field data-invalid={Boolean(errors.startupCommand)}>
            <FieldLabel className="mb-1.5 block">{t("Servers.StartupCommand")}</FieldLabel>
            <Input
              className="font-mono text-[13px]"
              placeholder={shard.data?.startupCommand ?? t("Servers.SelectShardFirst")}
              disabled={!shard.data}
              aria-invalid={errors.startupCommand ? "true" : undefined}
              {...register("startupCommand")}
            />
            <FieldDescription className="text-xs">{t("Servers.StartupCommandHint")}</FieldDescription>
            <FieldError errors={[errors.startupCommand]} />
          </Field>

          {shard.data && (
            <div className="space-y-3 border-t border-gray-100 pt-5 dark:border-gray-800/80">
              <h4 className="text-sm font-semibold text-gray-800 dark:text-white/90">{t("Servers.Variables")}</h4>
              <VariableFields
                variables={variables}
                errors={variables.map((_, index) => errors.variables?.[index]?.value?.message)}
                onChange={(index, value) => setValue(`variables.${index}.value`, value, { shouldDirty: true, shouldValidate: isSubmitted })}
              />
            </div>
          )}
        </SectionCard>

        <SectionCard icon={<ChipIcon />} title={t("Servers.Sections.Resources")} description={t("Servers.Sections.ResourcesHint")}>
          <ResourceFields form={form} />
        </SectionCard>

        <SectionCard icon={<LayerIcon />} title={t("Servers.Sections.FeatureLimits")} description={t("Servers.Sections.FeatureLimitsHint")}>
          <FeatureLimitFields form={form} />
        </SectionCard>
      </div>

      <div className="space-y-6 xl:sticky xl:top-24">
        <SectionCard icon={<GearIcon />} title={t("Servers.Sections.Options")}>
          <Controller
            control={control}
            name="startOnCompletion"
            render={({ field }) => (
              <OptionToggle checked={field.value} onChange={field.onChange} title={t("Servers.StartOnCompletion")} description={t("Servers.StartOnCompletionHint")} />
            )}
          />
          <Controller
            control={control}
            name="skipInstall"
            render={({ field }) => (
              <OptionToggle checked={field.value} onChange={field.onChange} title={t("Servers.SkipInstall")} description={t("Servers.SkipInstallHint")} />
            )}
          />
        </SectionCard>

        <SectionCard title={t("Servers.Summary")}>
          <DefinitionList rows={summary} />
          <Button type="submit" className="w-full" disabled={busy}>
            <PlusIcon />
            {t("Servers.Create")}
          </Button>
          <p className="text-center text-xs text-gray-500 dark:text-gray-400">{t("Servers.CreateHint")}</p>
        </SectionCard>
      </div>
    </form>
  );
}

export default function CreateServerPage() {
  const { t } = useT();

  return (
    <RequirePermission permission={Permissions.AdminServersWrite}>
      <PageHeader
        icon={<ChipIcon />}
        title={t("Servers.Create")}
        description={t("Servers.CreateDescription")}
        crumbs={[{ label: t("Nav.Admin.Servers"), to: "/admin/servers" }]}
      />
      <CreateServerForm />
    </RequirePermission>
  );
}
