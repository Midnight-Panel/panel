import { api, toQueryString } from "@/api/client";
import type { AdminUser, PagedResult } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { DataTable, type DataTableColumn } from "@/components/shared/data-table";
import { PasswordInput } from "@/components/shared/password-input";
import { UserAvatar } from "@/components/shared/user-avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n";
import { PencilIcon, PlusIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router";

interface CreateForm {
  username: string;
  email: string;
  password: string;
}

export default function UsersPage() {
  const { t, formatDate } = useT();
  const { can } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const { register, handleSubmit, reset, formState: { errors } } = useForm<CreateForm>({ mode: "onTouched" });

  const columns: DataTableColumn<AdminUser>[] = [
    { key: "id", title: "ID", sortKey: "id", render: (u) => <span className="text-gray-400">#{u.id}</span> },
    {
      key: "user", title: t("Common.User"), sortKey: "username",
      render: (u) => (
        <Link to={`/admin/users/${u.id}`} className="flex items-center gap-3">
          <UserAvatar userId={u.id} hasAvatar={u.hasAvatar} name={u.username} />
          <div>
            <span className="block text-sm font-medium text-gray-800 dark:text-white/90">{u.username}</span>
            <span className="block text-xs text-gray-500 dark:text-gray-400">{u.email}</span>
          </div>
        </Link>
      ),
    },
    { key: "roles", title: t("Users.Roles"), render: (u) => <div className="flex flex-wrap gap-1">{u.roleNames.map((r) => <Badge key={r} size="sm">{r}</Badge>)}</div> },
    {
      key: "status", title: t("Common.Status"),
      render: (u) => (
        <div className="flex flex-wrap gap-1">
          {u.isLocked ? <Badge size="sm" color="error">{t("Users.Locked")}</Badge> : <Badge size="sm" color="success">{t("Users.Active")}</Badge>}
          {u.twoFactorEnabled && <Badge size="sm" color="info">2FA</Badge>}
        </div>
      ),
    },
    { key: "created", title: t("Users.Created"), sortKey: "createdAt", render: (u) => formatDate(u.createdAt) },
    {
      key: "actions", title: "", align: "end",
      render: (u) => (
        <Button variant="ghost" size="icon-sm" render={<Link to={`/admin/users/${u.id}`} />} nativeButton={false} aria-label={t("Common.Edit")}>
          <PencilIcon />
        </Button>
      ),
    },
  ];

  const create = async (values: CreateForm) => {
    setBusy(true);

    try {
      const user = await api.post<AdminUser>("/api/admin/users", { ...values, roleIds: [] });
      await queryClient.invalidateQueries({ queryKey: ["admin", "users"] });
      setOpen(false);
      notifySuccess(t("Users.CreatedToast", user.username));
      navigate(`/admin/users/${user.id}`);
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <DataTable
        queryKey={["admin", "users"]}
        load={(query) => api.get<PagedResult<AdminUser>>(`/api/admin/users?${toQueryString(query)}`)}
        columns={columns}
        rowKey={(u) => u.id}
        title={t("Nav.Admin.Users")}
        toolbar={can(Permissions.AdminUsersWrite) && (
          <Button onClick={() => { reset(); setOpen(true); }}>
            <PlusIcon />
            {t("Users.Create")}
          </Button>
        )}
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("Users.Create")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSubmit(create)} className="space-y-5" noValidate>
            <Field data-invalid={Boolean(errors.username)}>
              <FieldLabel className="mb-1.5 block">{t("Common.Username")}</FieldLabel>
              <Input aria-invalid={errors.username ? "true" : undefined}
                     {...register("username", { pattern: { value: /^[a-zA-Z0-9_.-]{3,32}$/, message: t("Validation.Username") }, required: t("Validation.Username") })} />
              <FieldError errors={[errors.username]} />
            </Field>
            <Field data-invalid={Boolean(errors.email)}>
              <FieldLabel className="mb-1.5 block">{t("Common.Email")}</FieldLabel>
              <Input type="email" aria-invalid={errors.email ? "true" : undefined}
                     {...register("email", { required: t("Validation.Email"), pattern: { value: /^\S+@\S+$/, message: t("Validation.Email") } })} />
              <FieldError errors={[errors.email]} />
            </Field>
            <Field data-invalid={Boolean(errors.password)}>
              <FieldLabel className="mb-1.5 block">{t("Common.Password")}</FieldLabel>
              <PasswordInput autoComplete="new-password" aria-invalid={errors.password ? "true" : undefined}
                             {...register("password", { minLength: { value: 8, message: t("Security.PasswordHint") }, required: t("Security.PasswordHint") })} />
              <FieldError errors={[errors.password]} />
            </Field>
            <DialogFooter className="mx-0 mb-0 p-0">
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("Common.Cancel")}</Button>
              <Button type="submit" disabled={busy}>{t("Users.Create")}</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
