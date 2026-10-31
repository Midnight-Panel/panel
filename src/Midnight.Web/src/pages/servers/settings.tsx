import { serversApi } from "@/api/servers";
import { useAuth } from "@/auth/AuthContext";
import { DefinitionList, SectionCard } from "@/components/shared/section-card";
import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import { FloppyDiscIcon, FolderIcon, PencilIcon, RegenerateIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { ServerPermissions } from "@/lib/server-permissions";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { useMemo, useState, type ReactNode } from "react";
import { useForm } from "react-hook-form";
import * as z from "zod";
import { CopyButton } from "./components/badges";
import { serverQueryKey, useServerContext } from "./hooks";
import { applyProblemErrors } from "./schema";

function CopyValue({ value }: { value: string }) {
  return (
    <span className="inline-flex items-center gap-1 font-mono text-xs">
      {value}
      <CopyButton text={value} />
    </span>
  );
}

export default function ServerSettingsPage() {
  const { server, can } = useServerContext();
  const canRename = can(ServerPermissions.SettingsRename);
  const canReinstall = can(ServerPermissions.SettingsReinstall);
  const locked = !canRename || server.suspended;
  const { user } = useAuth();
  const { t } = useT();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState<"rename" | "reinstall" | null>(null);

  const schema = useMemo(
    () =>
      z.object({
        name: z.string().trim().min(1, t("Validation.Required")).max(64, t("Servers.Validation.MaxLength", 64)),
        description: z.string().max(512, t("Servers.Validation.MaxLength", 512)),
      }),
    [t],
  );
  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({
    mode: "onTouched",
    resolver: zodResolver(schema),
    defaultValues: { name: server.name, description: server.description ?? "" },
  });
  const { register, formState: { errors, isDirty } } = form;

  const rename = async (values: Values) => {
    setBusy("rename");

    try {
      const updated = await serversApi.rename(server.uuid, { name: values.name.trim(), description: values.description.trim() || null });
      queryClient.setQueryData(serverQueryKey(server.uuid), updated);
      await queryClient.invalidateQueries({ queryKey: ["servers", "list"] });
      form.reset(values);
      notifySuccess(t("Common.Saved"));
    } catch (error) {
      applyProblemErrors(form.setError, error, { Name: "name", Description: "description" });
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const reinstall = async () => {
    if (!(await confirm({
      title: t("Servers.Reinstall"),
      message: t("Servers.ReinstallConfirm", server.name),
      confirmLabel: t("Servers.Reinstall"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    setBusy("reinstall");

    try {
      await serversApi.reinstall(server.uuid);
      await queryClient.invalidateQueries({ queryKey: serverQueryKey(server.uuid) });
      notifySuccess(t("Servers.ReinstallStarted"));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const sftpUser = `${user?.username ?? "user"}.${server.uuid.slice(0, 8)}`;

  const sftp: Array<[string, ReactNode]> = [
    [t("Servers.SftpAddress"), <CopyValue key="address" value={`sftp://${server.nodeFqdn}:${server.sftpPort}`} />],
    [t("Servers.SftpUsername"), <CopyValue key="username" value={sftpUser} />],
    [t("Common.Password"), <span key="password" className="text-xs font-normal text-gray-500 dark:text-gray-400">{t("Servers.SftpPasswordHint")}</span>],
  ];

  return (
    <div className="grid grid-cols-1 items-start gap-6 lg:grid-cols-3">
      <form onSubmit={form.handleSubmit(rename)} noValidate className="lg:col-span-2">
        <SectionCard icon={<PencilIcon />} title={t("Servers.Sections.Details")} description={t("Servers.RenameHint")}>
          <Field data-invalid={Boolean(errors.name)}>
            <FieldLabel className="mb-1.5 block">{t("Common.Name")}</FieldLabel>
            <Input aria-invalid={errors.name ? "true" : undefined} disabled={locked} {...register("name")} />
            <FieldError errors={[errors.name]} />
          </Field>
          <Field data-invalid={Boolean(errors.description)}>
            <FieldLabel className="mb-1.5 block">{t("Common.Description")}</FieldLabel>
            <Textarea rows={3} aria-invalid={errors.description ? "true" : undefined} disabled={locked} {...register("description")} />
            <FieldError errors={[errors.description]} />
          </Field>
          {canRename && (
            <div className="flex justify-end">
              <Button type="submit" disabled={busy !== null || !isDirty || server.suspended}>
                <FloppyDiscIcon />
                {t("Common.SaveChanges")}
              </Button>
            </div>
          )}
        </SectionCard>
      </form>

      <div className="space-y-6">
        <SectionCard icon={<FolderIcon />} title={t("Servers.Sftp")} description={t("Servers.SftpHint")}>
          <DefinitionList rows={sftp} />
        </SectionCard>

        {canReinstall && (
          <SectionCard danger icon={<RegenerateIcon />} title={t("Servers.Reinstall")} description={t("Servers.ReinstallUserHint")}>
            <Button variant="destructive" className="w-full" disabled={busy !== null || server.suspended || server.status === "Installing"} onClick={reinstall}>
              <RegenerateIcon />
              {t("Servers.Reinstall")}
            </Button>
          </SectionCard>
        )}
      </div>
    </div>
  );
}
