import { serversApi } from "@/api/servers";
import type { ServerVariable } from "@/api/types";
import { DefinitionList, SectionCard } from "@/components/shared/section-card";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { BoxIcon, CodeIcon, FloppyDiscIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { ServerPermissions } from "@/lib/server-permissions";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import * as z from "zod";
import { CopyButton } from "./components/badges";
import { VariableFields } from "./components/variable-fields";
import { serverQueryKey, useServerContext } from "./hooks";
import { applyProblemErrors, variableErrorPaths, variablesFromServer, variablesSchema, variablesToRecord } from "./schema";

export default function ServerStartupPage() {
  const { server, can } = useServerContext();
  const canUpdate = can(ServerPermissions.StartupUpdate);
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const schema = useMemo(() => z.object({ variables: variablesSchema(t) }), [t]);
  type Values = z.infer<typeof schema>;

  const editable = canUpdate && !server.suspended;
  const isEditable = (variable: ServerVariable) => canUpdate && variable.userEditable;

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { variables: variablesFromServer(server.variables, isEditable) },
  });
  const { setValue, control, formState: { errors, isDirty, isSubmitted } } = form;
  const variables = useWatch({ control, name: "variables" });
  const hasEditable = variables.some((variable) => variable.editable);

  const save = async (values: Values) => {
    setBusy(true);

    try {
      const updated = await serversApi.updateVariables(server.uuid, { variables: variablesToRecord(values.variables) });
      queryClient.setQueryData(serverQueryKey(server.uuid), updated);
      form.reset({ variables: variablesFromServer(updated.variables, isEditable) });
      notifySuccess(t("Servers.VariablesSaved"));
    } catch (error) {
      applyProblemErrors(form.setError, error, variableErrorPaths(values.variables));
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={form.handleSubmit(save)} noValidate className="space-y-6">
      <SectionCard icon={<CodeIcon />} title={t("Servers.StartupCommand")} description={t("Servers.StartupPreviewHint")}>
        <div className="relative">
          <pre className="overflow-x-auto rounded-xl border border-gray-200 bg-gray-50 p-4 pe-12 font-mono text-[13px] leading-relaxed whitespace-pre-wrap text-gray-800 dark:border-gray-800 dark:bg-white/[0.03] dark:text-gray-200">
            {server.startupPreview}
          </pre>
          <CopyButton text={server.startupPreview} className="absolute end-2 top-2" />
        </div>
        <DefinitionList rows={[[t("Servers.DockerImage"), <span key="image" className="font-mono text-xs">{server.dockerImage}</span>]]} />
      </SectionCard>

      <SectionCard
        icon={<BoxIcon />}
        title={t("Servers.Variables")}
        description={hasEditable ? t("Servers.VariablesUserHint") : t("Servers.VariablesReadOnlyHint")}
        actions={hasEditable && (
          <Button type="submit" size="sm" disabled={busy || !isDirty || !editable}>
            <FloppyDiscIcon className="size-4" />
            {t("Common.SaveChanges")}
          </Button>
        )}
      >
        <VariableFields
          variables={variables}
          disabled={!editable}
          errors={variables.map((_, index) => errors.variables?.[index]?.value?.message)}
          onChange={(index, value) => setValue(`variables.${index}.value`, value, { shouldDirty: true, shouldValidate: isSubmitted })}
        />
      </SectionCard>
    </form>
  );
}
