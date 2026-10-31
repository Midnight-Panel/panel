import { api } from "@/api/client";
import type { Role, RoleRequest } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { tableStyles } from "@/components/shared/data-table";
import { RequirePermission } from "@/components/shared/guards";
import { PermissionPicker } from "@/components/shared/permission-picker";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHeader, TableRow } from "@/components/ui/table";
import { useT } from "@/i18n";
import { PencilIcon, PlusIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { PermissionGroups, Permissions } from "@/lib/permissions";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";

function RolesContent() {
  const { t } = useT();
  const { can, reload } = useAuth();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<Role | null | undefined>(undefined);
  const [busy, setBusy] = useState(false);
  const canWrite = can(Permissions.AdminRolesWrite);

  const roles = useQuery({ queryKey: ["admin", "roles"], queryFn: () => api.get<Role[]>("/api/admin/roles") });
  const { register, handleSubmit, reset, watch, setValue, formState: { errors } } = useForm<RoleRequest>();

  const open = (role: Role | null) => {
    reset({ name: role?.name ?? "", description: role?.description ?? "", permissions: [...(role?.permissions ?? [])] });
    setEditing(role);
  };

  const save = async (values: RoleRequest) => {
    setBusy(true);

    try {
      if (editing) await api.put(`/api/admin/roles/${editing.id}`, values);
      else await api.post("/api/admin/roles", values);

      setEditing(undefined);
      notifySuccess(t("Common.Saved"));
      await queryClient.invalidateQueries({ queryKey: ["admin", "roles"] });
      await reload();
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const remove = async (role: Role) => {
    if (!(await confirm({ title: t("Common.Delete"), message: t("Roles.DeleteConfirm", role.name), confirmLabel: t("Common.Delete"), cancelLabel: t("Common.Cancel") })))
      return;

    try {
      await api.delete(`/api/admin/roles/${role.id}`);
      await queryClient.invalidateQueries({ queryKey: ["admin", "roles"] });
    } catch (error) {
      notifyError(error);
    }
  };

  return (
    <>
      <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/3">
        <div className="flex flex-col gap-3 border-b border-gray-100 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6 dark:border-gray-800">
          <div>
            <h3 className="text-lg font-semibold text-gray-800 dark:text-white/90">{t("Nav.Admin.Roles")}</h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{t("Roles.Description")}</p>
          </div>
          {canWrite && (
            <Button onClick={() => open(null)}>
              <PlusIcon />
              {t("Roles.Create")}
            </Button>
          )}
        </div>
        <div className="max-w-full overflow-x-auto">
          <Table>
            <TableHeader className="border-b border-gray-100 dark:border-white/5">
              <TableRow>
                <TableCell isHeader className={tableStyles.headerCell}>{t("Common.Name")}</TableCell>
                <TableCell isHeader className={tableStyles.headerCell}>{t("Roles.Permissions")}</TableCell>
                <TableCell isHeader className={tableStyles.headerCell}>{t("Nav.Admin.Users")}</TableCell>
                <TableCell isHeader className={tableStyles.headerCell}>{" "}</TableCell>
              </TableRow>
            </TableHeader>
            <TableBody className="divide-y divide-gray-100 dark:divide-white/5">
              {(roles.data ?? []).map((role) => (
                <TableRow key={role.id}>
                  <TableCell className={tableStyles.bodyCell}>
                    <span className="block font-medium text-gray-800 dark:text-white/90">{role.name}</span>
                    {role.description && <span className="block text-xs">{role.description}</span>}
                  </TableCell>
                  <TableCell className={tableStyles.bodyCell}>
                    <div className="flex flex-wrap gap-1">
                      {role.permissions.slice(0, 6).map((p) => (
                        <Badge key={p} size="sm" className="font-mono" color={p === Permissions.Everything ? "error" : "primary"}>{p}</Badge>
                      ))}
                      {role.permissions.length > 6 && <Badge size="sm" color="gray">+{role.permissions.length - 6}</Badge>}
                    </div>
                  </TableCell>
                  <TableCell className={tableStyles.bodyCell}>{role.userCount}</TableCell>
                  <TableCell className={`${tableStyles.bodyCell} text-end`}>
                    {canWrite && (
                      <div className="flex justify-end gap-1">
                        <Button variant="ghost" size="icon-sm" onClick={() => open(role)} aria-label={t("Common.Edit")}><PencilIcon /></Button>
                        <Button variant="ghost" size="icon-sm" className="text-red-500" onClick={() => remove(role)} aria-label={t("Common.Delete")}><TrashBinIcon /></Button>
                      </div>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
        {roles.data?.length === 0 && <p className="py-12 text-center text-sm text-gray-500 dark:text-gray-400">{t("Roles.Empty")}</p>}
      </div>

      <Dialog open={editing !== undefined} onOpenChange={(value) => !value && setEditing(undefined)}>
        <DialogContent className="sm:max-w-3xl">
          <DialogHeader>
            <DialogTitle>{editing ? t("Roles.Edit") : t("Roles.Create")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSubmit(save)} className="space-y-5" noValidate>
            <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
              <Field data-invalid={Boolean(errors.name)}>
                <FieldLabel className="mb-1.5 block">{t("Common.Name")}</FieldLabel>
                <Input aria-invalid={errors.name ? "true" : undefined} {...register("name", { required: t("Validation.Required") })} />
                <FieldError errors={[errors.name]} />
              </Field>
              <Field>
                <FieldLabel className="mb-1.5 block">{t("Common.Description")}</FieldLabel>
                <Input {...register("description")} />
              </Field>
            </div>
            <div>
              <FieldLabel className="mb-2 block">{t("Roles.Permissions")}</FieldLabel>
              <div className="max-h-[45vh] overflow-y-auto pe-1">
                <PermissionPicker value={watch("permissions") ?? []} onChange={(value) => setValue("permissions", value)} groups={PermissionGroups} allowEverything />
              </div>
            </div>
            <DialogFooter className="mx-0 mb-0 p-0">
              <Button type="button" variant="outline" onClick={() => setEditing(undefined)}>{t("Common.Cancel")}</Button>
              <Button type="submit" disabled={busy}>{t("Common.Save")}</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}

export default function RolesPage() {
  return (
    <RequirePermission permission={Permissions.AdminRolesRead}>
      <RolesContent />
    </RequirePermission>
  );
}
