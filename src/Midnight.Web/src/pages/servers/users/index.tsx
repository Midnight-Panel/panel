import { serverFeatureKeys, serverPermissionsApi, subusersApi, type Subuser } from "@/api/server-features";
import { EmptyState } from "@/components/shared/empty-state";
import { UserAvatar } from "@/components/shared/user-avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n";
import { GroupIcon, PencilIcon, PlusIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { ServerPermissions } from "@/lib/server-permissions";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import * as z from "zod";
import { CardLoader, FeatureCard, RelativeTime, ServerPermissionGate } from "../components/feature-ui";
import { useServerContext } from "../hooks";
import { applyProblemErrors } from "../schema";
import { PermissionEditor } from "./permission-editor";

type DialogState = { mode: "create" } | { mode: "edit"; subuser: Subuser } | null;

function SubuserDialog({ state, onClose }: { state: DialogState; onClose: () => void }) {
  const { server, can } = useServerContext();
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);
  const groups = useQuery({ queryKey: ["servers", "permissions"], queryFn: serverPermissionsApi.groups, staleTime: Infinity });
  const editing = state?.mode === "edit" ? state.subuser : null;

  const schema = useMemo(
    () =>
      z.object({
        email: editing ? z.string() : z.string().trim().min(1, t("Validation.Required")).email(t("Validation.Email")).max(255),
        permissions: z.array(z.string()).min(1, t("ServerUsers.PermissionRequired")),
      }),
    [t, editing],
  );
  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({
    resolver: zodResolver(schema),
    values: { email: editing?.email ?? "", permissions: editing?.permissions ?? [] },
  });
  const { register, control, setValue, formState: { errors, isSubmitted } } = form;
  const permissions = useWatch({ control, name: "permissions" });

  const save = async (values: Values) => {
    setBusy(true);

    try {
      if (editing) {
        await subusersApi.update(server.uuid, editing.id, { permissions: values.permissions });
        notifySuccess(t("ServerUsers.Updated", editing.username));
      } else {
        const created = await subusersApi.create(server.uuid, { email: values.email.trim(), permissions: values.permissions });
        notifySuccess(t("ServerUsers.Invited", created.username));
      }

      await queryClient.invalidateQueries({ queryKey: serverFeatureKeys.subusers(server.uuid) });
      onClose();
    } catch (error) {
      applyProblemErrors(form.setError, error, { Email: "email", Permissions: "permissions" });
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={state !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>{editing ? t("ServerUsers.EditTitle", editing.username) : t("ServerUsers.Invite")}</DialogTitle>
          <DialogDescription>{editing ? t("ServerUsers.EditHint") : t("ServerUsers.InviteHint")}</DialogDescription>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(save)} noValidate className="space-y-5">
          {!editing && (
            <Field data-invalid={Boolean(errors.email)}>
              <FieldLabel className="mb-1.5 block">{t("Common.Email")}</FieldLabel>
              <Input type="email" autoComplete="off" placeholder="name@example.com" aria-invalid={errors.email ? "true" : undefined} {...register("email")} />
              <FieldDescription>{t("ServerUsers.EmailHint")}</FieldDescription>
              <FieldError errors={[errors.email]} />
            </Field>
          )}

          <Field data-invalid={Boolean(errors.permissions)}>
            <div className="mb-2 flex items-center justify-between gap-3">
              <FieldLabel>{t("ServerUsers.Permissions")}</FieldLabel>
              <span className="text-xs text-gray-500 dark:text-gray-400">{t("ServerUsers.SelectedCount", permissions.length)}</span>
            </div>
            <PermissionEditor
              value={permissions}
              groups={groups.data}
              grantable={can}
              onChange={(value) => setValue("permissions", value, { shouldDirty: true, shouldValidate: isSubmitted })}
            />
            <FieldError errors={[errors.permissions]} />
          </Field>

          <DialogFooter className="mx-0 mb-0 p-0">
            <Button type="button" variant="outline" onClick={onClose}>{t("Common.Cancel")}</Button>
            <Button type="submit" disabled={busy}>
              {editing ? t("Common.SaveChanges") : t("ServerUsers.Invite")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function SubuserList() {
  const { server, can } = useServerContext();
  const { t } = useT();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [dialog, setDialog] = useState<DialogState>(null);

  const { data, isLoading } = useQuery({ queryKey: serverFeatureKeys.subusers(server.uuid), queryFn: () => subusersApi.list(server.uuid) });
  const subusers = data ?? [];

  const remove = async (subuser: Subuser) => {
    if (!(await confirm({
      title: t("ServerUsers.Remove"),
      message: t("ServerUsers.RemoveConfirm", subuser.username),
      confirmLabel: t("Common.Remove"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    try {
      await subusersApi.delete(server.uuid, subuser.id);
      await queryClient.invalidateQueries({ queryKey: serverFeatureKeys.subusers(server.uuid) });
      notifySuccess(t("ServerUsers.Removed", subuser.username));
    } catch (error) {
      notifyError(error);
    }
  };

  const inviteButton = can(ServerPermissions.UserCreate) && (
    <Button size="sm" onClick={() => setDialog({ mode: "create" })} disabled={server.suspended}>
      <PlusIcon className="size-4" />
      {t("ServerUsers.Invite")}
    </Button>
  );

  return (
    <>
      {isLoading ? (
        <FeatureCard title={t("ServerUsers.Title")}><CardLoader /></FeatureCard>
      ) : subusers.length === 0 ? (
        <EmptyState icon={<GroupIcon />} title={t("ServerUsers.Empty")} description={t("ServerUsers.EmptyHint")}>
          {inviteButton && <div className="mt-6">{inviteButton}</div>}
        </EmptyState>
      ) : (
        <FeatureCard title={t("ServerUsers.Title")} description={t("ServerUsers.Description")} actions={inviteButton}>
          <ul className="divide-y divide-gray-100 dark:divide-white/5">
            {subusers.map((subuser) => (
              <li key={subuser.id} className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:px-6">
                <div className="flex min-w-0 flex-1 items-center gap-3">
                  <UserAvatar userId={subuser.userId} hasAvatar={subuser.hasAvatar} name={subuser.username} />
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-gray-800 dark:text-white/90">{subuser.username}</p>
                    <p className="truncate text-xs text-gray-500 dark:text-gray-400">{subuser.email}</p>
                  </div>
                </div>
                <div className="flex items-center gap-3 sm:gap-4">
                  <Badge size="sm" color="primary">{t("ServerUsers.PermissionCount", subuser.permissions.length)}</Badge>
                  <span className="hidden text-xs text-gray-500 md:inline dark:text-gray-400">
                    {t("ServerUsers.Added")} <RelativeTime value={subuser.createdAt} />
                  </span>
                  <div className="ms-auto flex items-center gap-1">
                    {can(ServerPermissions.UserUpdate) && (
                      <Button variant="ghost" size="icon-sm" onClick={() => setDialog({ mode: "edit", subuser })} aria-label={t("Common.Edit")} title={t("Common.Edit")}>
                        <PencilIcon className="size-4" />
                      </Button>
                    )}
                    {can(ServerPermissions.UserDelete) && (
                      <Button variant="ghost" size="icon-sm" className="text-red-500 dark:text-red-400" onClick={() => remove(subuser)} aria-label={t("Common.Remove")} title={t("Common.Remove")}>
                        <TrashBinIcon className="size-4" />
                      </Button>
                    )}
                  </div>
                </div>
              </li>
            ))}
          </ul>
        </FeatureCard>
      )}

      <SubuserDialog key={dialog?.mode === "edit" ? dialog.subuser.id : (dialog?.mode ?? "closed")} state={dialog} onClose={() => setDialog(null)} />
    </>
  );
}

export default function ServerUsersPage() {
  return (
    <ServerPermissionGate permission={ServerPermissions.UserRead}>
      <SubuserList />
    </ServerPermissionGate>
  );
}
