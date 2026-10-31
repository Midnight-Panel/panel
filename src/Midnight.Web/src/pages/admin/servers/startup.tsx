import { adminServersApi } from "@/api/servers";
import { shardsApi } from "@/api/shards";
import { SectionCard } from "@/components/shared/section-card";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n";
import { BoxIcon, CodeIcon, FloppyDiscIcon, InfoCircleIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { VariableFields } from "@/pages/servers/components/variable-fields";
import {
  applyProblemErrors,
  variableErrorPaths,
  variablesFromServer,
  variablesFromShard,
  variablesSchema,
  variablesToRecord,
} from "@/pages/servers/schema";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import * as z from "zod";
import { DockerImageSelect, ShardSelect } from "./components/selects";
import { adminServerQueryKey, useAdminServer, useShardDetail } from "./hooks";

export default function AdminServerStartupPage() {
  const server = useAdminServer();
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const schema = useMemo(
    () =>
      z.object({
        shardId: z.number().int().min(1, t("Validation.Required")),
        dockerImage: z.string().trim().min(1, t("Validation.Required")).max(255),
        startupCommand: z.string().max(4096, t("Servers.Validation.MaxLength", 4096)),
        variables: variablesSchema(t),
      }),
    [t],
  );

  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({
    mode: "onTouched",
    resolver: zodResolver(schema),
    defaultValues: {
      shardId: server.shardId,
      dockerImage: server.dockerImage,
      startupCommand: server.startupCommand ?? "",
      variables: variablesFromServer(server.variables, () => true),
    },
  });
  const { register, setValue, getValues, control, formState: { errors, isDirty, isSubmitted } } = form;
  const [shardId, dockerImage, variables] = useWatch({ control, name: ["shardId", "dockerImage", "variables"] });
  const shard = useShardDetail(shardId);
  const shardChanged = shardId !== server.shardId;

  const images = shardChanged ? (shard.data?.dockerImages ?? {}) : server.shardDockerImages;
  const placeholder = shardChanged ? (shard.data?.startupCommand ?? "") : server.shardStartupCommand;

  const selectShard = async (id: number) => {
    setValue("shardId", id, { shouldDirty: true });

    try {
      const detail = await queryClient.fetchQuery({ queryKey: ["admin", "shards", id], queryFn: () => shardsApi.getShard(id) });

      // Same as the api: matching variables keep their value, new ones start with the default
      const current = new Map(getValues("variables").map((variable) => [variable.envVariable, variable.value]));
      const next = variablesFromShard(detail.variables).map((variable) => ({ ...variable, value: current.get(variable.envVariable) ?? variable.value }));

      setValue("variables", next, { shouldDirty: true });
      setValue("dockerImage", detail.defaultDockerImage || Object.values(detail.dockerImages)[0] || "", { shouldDirty: true });
      setValue("startupCommand", "", { shouldDirty: true });
    } catch (error) {
      notifyError(error);
    }
  };

  const save = async (values: Values) => {
    setBusy(true);

    try {
      const updated = await adminServersApi.updateStartup(server.id, {
        shardId: values.shardId,
        dockerImage: values.dockerImage,
        startupCommand: values.startupCommand.trim() || null,
        variables: variablesToRecord(values.variables),
      });
      queryClient.setQueryData(adminServerQueryKey(server.id), updated);
      await queryClient.invalidateQueries({ queryKey: ["admin", "servers", "list"] });
      await queryClient.invalidateQueries({ queryKey: ["servers", server.uuid] });
      form.reset({
        shardId: updated.shardId,
        dockerImage: updated.dockerImage,
        startupCommand: updated.startupCommand ?? "",
        variables: variablesFromServer(updated.variables, () => true),
      });
      notifySuccess(t("Servers.StartupSaved"));
    } catch (error) {
      applyProblemErrors(form.setError, error, {
        ...variableErrorPaths(values.variables),
        ShardId: "shardId",
        DockerImage: "dockerImage",
        StartupCommand: "startupCommand",
      });
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={form.handleSubmit(save)} noValidate className="space-y-6">
      <SectionCard icon={<BoxIcon />} title={t("Servers.Sections.Shard")} description={t("Servers.StartupTabHint")}>
        {shardChanged && (
          <Alert variant="warning" className="px-4 py-3">
            <InfoCircleIcon />
            <AlertDescription>{t("Servers.ShardChangeHint")}</AlertDescription>
          </Alert>
        )}
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <Field data-invalid={Boolean(errors.shardId)}>
            <FieldLabel className="mb-1.5 block">{t("Servers.Shard")}</FieldLabel>
            <ShardSelect value={shardId} onChange={(id) => void selectShard(id)} invalid={Boolean(errors.shardId)} />
            <FieldError>{errors.shardId?.message}</FieldError>
          </Field>
          <Field data-invalid={Boolean(errors.dockerImage)}>
            <FieldLabel className="mb-1.5 block">{t("Servers.DockerImage")}</FieldLabel>
            <DockerImageSelect
              images={images}
              value={dockerImage}
              onChange={(image) => setValue("dockerImage", image, { shouldDirty: true, shouldValidate: isSubmitted })}
            />
            <FieldError>{errors.dockerImage?.message}</FieldError>
          </Field>
        </div>
        <Field data-invalid={Boolean(errors.startupCommand)}>
          <FieldLabel className="mb-1.5 block">{t("Servers.StartupCommand")}</FieldLabel>
          <Input className="font-mono text-[13px]" placeholder={placeholder} aria-invalid={errors.startupCommand ? "true" : undefined} {...register("startupCommand")} />
          <FieldDescription className="text-xs">{t("Servers.StartupCommandHint")}</FieldDescription>
          <FieldError errors={[errors.startupCommand]} />
        </Field>
      </SectionCard>

      <SectionCard icon={<CodeIcon />} title={t("Servers.Variables")} description={t("Servers.VariablesAdminHint")}>
        <VariableFields
          variables={variables}
          errors={variables.map((_, index) => errors.variables?.[index]?.value?.message)}
          onChange={(index, value) => setValue(`variables.${index}.value`, value, { shouldDirty: true, shouldValidate: isSubmitted })}
        />
      </SectionCard>

      <div className="flex justify-end">
        <Button type="submit" disabled={busy || !isDirty}>
          <FloppyDiscIcon />
          {t("Common.SaveChanges")}
        </Button>
      </div>
    </form>
  );
}
