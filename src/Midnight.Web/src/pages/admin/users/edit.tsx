import { api } from "@/api/client";
import type { AdminUser, ImpersonationResponse, Role } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { PageLoader } from "@/components/shared/guards";
import { PasswordInput } from "@/components/shared/password-input";
import { UserAvatar } from "@/components/shared/user-avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import { ArrowLeftIcon, CheckIcon, EyeIcon, FloppyDiscIcon, LockIcon, PlusIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { cn } from "@/utils";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate, useParams } from "react-router";

interface EditForm {
  username: string;
  email: string;
  password: string;
  isLocked: boolean;
  roleIds: number[];
}

export default function UserEditPage() {
  const { id } = useParams();
  const { t, formatDate } = useT();
  const { user: me, can, isImpersonating, startImpersonation, reload } = useAuth();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const canWrite = can(Permissions.AdminUsersWrite);
  const canManageRoles = can(Permissions.AdminRolesWrite);

  const user = useQuery({ queryKey: ["admin", "users", id], queryFn: () => api.get<AdminUser>(`/api/admin/users/${id}`) });
  const roles = useQuery({ queryKey: ["admin", "roles"], queryFn: () => api.get<Role[]>("/api/admin/roles"), enabled: can(Permissions.AdminRolesRead) });

  const { register, handleSubmit, reset, watch, setValue, formState: { errors } } = useForm<EditForm>({ mode: "onTouched" });

  useEffect(() => {
    if (user.data) reset({ username: user.data.username, email: user.data.email, password: "", isLocked: user.data.isLocked, roleIds: user.data.roleIds });
  }, [user.data, reset]);

  useEffect(() => {
    if (user.error) {
      notifyError(user.error);
      navigate("/admin/users");
    }
  }, [user.error, navigate]);

  if (!user.data) return <PageLoader />;

  const data = user.data;
  const roleIds = watch("roleIds") ?? [];

  const save = async (values: EditForm) => {
    setBusy(true);

    try {
      await api.put<AdminUser>(`/api/admin/users/${data.id}`, {
        username: values.username,
        email: values.email,
        password: values.password || null,
        isLocked: values.isLocked,
        roleIds: values.roleIds,
      });

      await queryClient.invalidateQueries({ queryKey: ["admin", "users"] });
      notifySuccess(t("Common.Saved"));

      // Own permissions may have changed
      if (data.id === me?.id) await reload();
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const resetTwoFactor = async () => {
    if (!(await confirm({ title: t("Users.ResetTwoFactor"), message: t("Users.ResetTwoFactorConfirm", data.username), confirmLabel: t("Users.ResetTwoFactor"), cancelLabel: t("Common.Cancel") })))
      return;

    try {
      await api.post(`/api/admin/users/${data.id}/reset-2fa`);
      await queryClient.invalidateQueries({ queryKey: ["admin", "users"] });
      notifySuccess(t("Common.Saved"));
    } catch (error) {
      notifyError(error);
    }
  };

  const remove = async () => {
    if (!(await confirm({ title: t("Users.Delete"), message: t("Users.DeleteConfirm", data.username), confirmLabel: t("Common.Delete"), cancelLabel: t("Common.Cancel") })))
      return;

    try {
      await api.delete(`/api/admin/users/${data.id}`);
      await queryClient.invalidateQueries({ queryKey: ["admin", "users"] });
      notifySuccess(t("Users.Deleted"));
      navigate("/admin/users");
    } catch (error) {
      notifyError(error);
    }
  };

  const impersonate = async () => {
    try {
      await startImpersonation(await api.post<ImpersonationResponse>(`/api/admin/users/${data.id}/impersonate`));
      navigate("/");
    } catch (error) {
      notifyError(error);
    }
  };

  const toggleRole = (roleId: number) =>
    setValue("roleIds", roleIds.includes(roleId) ? roleIds.filter((x) => x !== roleId) : [...roleIds, roleId]);

  return (
    <div className="space-y-6">
      <div className="rounded-2xl border border-gray-200 bg-white p-5 lg:p-6 dark:border-gray-800 dark:bg-white/3">
        <div className="flex flex-col gap-5 sm:flex-row sm:items-center">
          <UserAvatar userId={data.id} hasAvatar={data.hasAvatar} name={data.username} className="size-16! text-lg" />
          <div className="flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <h4 className="text-lg font-semibold text-gray-800 dark:text-white/90">{data.username}</h4>
              {data.isLocked ? <Badge size="sm" color="error">{t("Users.Locked")}</Badge> : <Badge size="sm" color="success">{t("Users.Active")}</Badge>}
              {data.twoFactorEnabled && <Badge size="sm" color="info">2FA</Badge>}
            </div>
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{data.email}</p>
            <p className="mt-1 text-xs text-gray-400">
              {t("Users.Created")}: {formatDate(data.createdAt)} · {t("Users.LastLogin")}: {formatDate(data.lastLoginAt)}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" render={<Link to="/admin/users" />} nativeButton={false}>
              <ArrowLeftIcon />
              {t("Common.Back")}
            </Button>
            {can(Permissions.AdminUsersImpersonate) && data.id !== me?.id && !isImpersonating && (
              <Button variant="outline" onClick={impersonate}>
                <EyeIcon />
                {t("Users.Impersonate")}
              </Button>
            )}
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="rounded-2xl border border-gray-200 bg-white p-5 lg:col-span-2 lg:p-6 dark:border-gray-800 dark:bg-white/3">
          <h4 className="mb-6 text-lg font-semibold text-gray-800 dark:text-white/90">{t("Profile.Details")}</h4>
          <form onSubmit={handleSubmit(save)} className="space-y-5" noValidate>
            <fieldset disabled={!canWrite} className="space-y-5">
              <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
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
              </div>

              <Field data-invalid={Boolean(errors.password)}>
                <FieldLabel className="mb-1.5 block">{t("Users.NewPassword")}</FieldLabel>
                <PasswordInput autoComplete="new-password" aria-invalid={errors.password ? "true" : undefined}
                               {...register("password", { validate: (value) => !value || value.length >= 8 || t("Security.PasswordHint") })} />
                <FieldDescription>{t("Users.NewPasswordHint")}</FieldDescription>
                <FieldError errors={[errors.password]} />
              </Field>

              <div>
                <FieldLabel className="mb-2 block">{t("Users.Roles")}</FieldLabel>
                {roles.data?.length === 0 && <p className="text-sm text-gray-500 dark:text-gray-400">{t("Roles.Empty")}</p>}
                <div className="flex flex-wrap gap-2">
                  {(roles.data ?? []).map((role) => {
                    const selected = roleIds.includes(role.id);

                    return (
                      <button
                        key={role.id}
                        type="button"
                        disabled={!canManageRoles}
                        onClick={() => toggleRole(role.id)}
                        className={cn(
                          "inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-sm font-medium transition disabled:cursor-not-allowed disabled:opacity-60",
                          selected
                            ? "border-brand-500 bg-brand-50 text-brand-500 dark:bg-brand-500/15 dark:text-brand-400"
                            : "border-gray-300 text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-white/5",
                        )}
                      >
                        {selected ? <CheckIcon className="size-4" /> : <PlusIcon className="size-4" />}
                        {role.name}
                      </button>
                    );
                  })}
                </div>
                {!canManageRoles && <p className="mt-2 text-xs text-gray-500 dark:text-gray-400">{t("Users.RolesNeedPermission")}</p>}
              </div>

              <label className="flex cursor-pointer items-center gap-3">
                <Switch checked={watch("isLocked") ?? false} onCheckedChange={(checked) => setValue("isLocked", Boolean(checked))} disabled={!canWrite} />
                <span className="text-sm text-gray-700 dark:text-gray-400">{t("Users.LockAccount")}</span>
              </label>
            </fieldset>

            {canWrite && (
              <div className="flex justify-end">
                <Button type="submit" disabled={busy}>
                  <FloppyDiscIcon />
                  {t("Common.SaveChanges")}
                </Button>
              </div>
            )}
          </form>
        </div>

        {canWrite && (
          <div className="rounded-2xl border border-red-200 bg-white p-5 lg:p-6 dark:border-red-500/30 dark:bg-white/3">
            <h4 className="mb-2 text-lg font-semibold text-red-600 dark:text-red-500">{t("Users.DangerZone")}</h4>
            <p className="mb-6 text-sm text-gray-500 dark:text-gray-400">{t("Users.DangerZoneHint")}</p>
            <div className="space-y-3">
              {data.twoFactorEnabled && (
                <Button variant="outline" className="w-full" onClick={resetTwoFactor}>
                  <LockIcon />
                  {t("Users.ResetTwoFactor")}
                </Button>
              )}
              <Button variant="destructive" className="w-full" onClick={remove} disabled={data.id === me?.id}>
                <TrashBinIcon />
                {t("Users.Delete")}
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
